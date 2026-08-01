using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Nuotti.AudioEngine;
using Nuotti.AudioEngine.AudioDevices;
using Nuotti.AudioEngine.Output;
using Nuotti.AudioEngine.Playback;
using Nuotti.Contracts.V1.Enum;
using Nuotti.Contracts.V1.Message;
using System.Text.Json;
using Serilog;

static string GetArg(string[] args, string name, string? envVar = null, string? fallback = null)
{
    for (int i = 0; i < args.Length; i++)
    {
        if ((args[i] == $"--{name}" || args[i] == $"-{name[0]}") && i + 1 < args.Length)
            return args[i + 1];
        var prefix = $"--{name}=";
        if (args[i].StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
            return args[i].Substring(prefix.Length);
    }
    var fromEnv = !string.IsNullOrWhiteSpace(envVar) ? Environment.GetEnvironmentVariable(envVar) : null;
    return !string.IsNullOrWhiteSpace(fromEnv) ? fromEnv! : (fallback ?? string.Empty);
}

// Load engine options from engine.json and environment (NUOTTI_ENGINE__*)
var configuration = new ConfigurationBuilder()
    .SetBasePath(AppContext.BaseDirectory)
    .AddJsonFile("engine.json", optional: true, reloadOnChange: false)
    .AddEnvironmentVariables(prefix: "NUOTTI_ENGINE__")
    .Build();

ServiceDefaults.NuottiHost.ConfigureNuottiProcess("Nuotti.AudioEngine", configuration);

var engineOptions = new EngineOptions();
configuration.Bind(engineOptions);

// Validate configuration with helpful error messages
try
{
    engineOptions.Validate();
}
catch (ArgumentException ex)
{
    Log.Fatal(ex, "Configuration validation failed: {Message}. Hint: Check engine.json or NUOTTI_ENGINE__* environment variables", ex.Message);
    Environment.Exit(1);
}

Log.Information("Engine effective config: {Config}", JsonSerializer.Serialize(engineOptions, new JsonSerializerOptions { WriteIndented = true }));

// Metrics setup
var metrics = new AudioEngineMetrics();
_ = MetricsHost.RunIfEnabledAsync(engineOptions.Metrics, metrics, CancellationToken.None);

var backend = GetArg(args, "backend", envVar: "NUOTTI_BACKEND", fallback: "http://localhost:5240");
var session = GetArg(args, "session", envVar: "NUOTTI_SESSION", fallback: "dev");

Log.Information("AudioEngine target. Backend={Backend}, Session={Session}", backend, session);

var cts = new CancellationTokenSource();
Console.CancelKeyPress += (_, e) => { e.Cancel = true; cts.Cancel(); };

// Setup DI for audio backends and create the audio player based on config
var services = new ServiceCollection();
services.AddAudioBackends(configuration);
var provider = services.BuildServiceProvider();
var optsFromDi = provider.GetRequiredService<IOptions<EngineOptions>>().Value;
var audioBackend = provider.GetRequiredService<IAudioBackend>();
var backendType = audioBackend.GetType().Name;
IAudioPlayer player = audioBackend.CreatePlayer(optsFromDi);
player.Started += (_, __) => { Log.Information("Playback started"); metrics.SetPlaying(currentFile: null); };
player.Stopped += (_, cancelled) => { Log.Information("Playback stopped. Cancelled={Cancelled}", cancelled); metrics.SetStopped(); };
player.Error += (_, ex) => { Log.Error(ex, "Playback error: {Message}", ex.Message); metrics.SetError(ex.Message); };

var connection = new HubConnectionBuilder()
    .WithUrl(new Uri(new Uri(backend), "/hub"))
    .WithAutomaticReconnect()
    .Build();

var httpClient = new HttpClient();
httpClient.BaseAddress = new Uri(backend);
var pairCode = GetArg(args, "pair-code", envVar: "NUOTTI_PAIR_CODE");
var agentName = GetArg(args, "agent-name", envVar: "NUOTTI_AGENT_NAME", fallback: Environment.MachineName);
ShowAgentCloudClient? cloudAgent = null;
if (OperatingSystem.IsWindows())
{
    var credentialStore = FileShowAgentCredentialStore.CreateDefault();
    if (!string.IsNullOrWhiteSpace(pairCode) || credentialStore.Load() is not null)
        cloudAgent = new ShowAgentCloudClient(httpClient, credentialStore);
}
IEngineStatusSink sink = cloudAgent is null
    ? new HubStatusSink(connection, session)
    : new CloudAgentStatusSink(cloudAgent);
IProblemSink problemSink = cloudAgent is null
    ? new HubProblemSink(connection, session)
    : new CloudAgentProblemSink(cloudAgent);
ISourcePreflight preflight = new HttpFilePreflight(httpClient, options: engineOptions.Safety);
var engine = new EngineCoordinator(player, sink, preflight, problemSink);

// Audio device enumeration (foundation)
IAudioDeviceEnumerator deviceEnumerator = new BasicAudioDeviceEnumerator();

// Log backend and device info on startup
Log.Information("AudioEngine backend: {BackendType}, OutputBackend={OutputBackend}, OutputDevice={OutputDevice}, PreferredPlayer={PreferredPlayer}", 
    backendType, engineOptions.OutputBackend ?? "default", engineOptions.OutputDevice ?? "default", engineOptions.PreferredPlayer);

try
{
    var devices = await deviceEnumerator.EnumerateAsync(cts.Token);
    Log.Information("Audio devices (default={DefaultDeviceId}):", devices.DefaultDeviceId);
    foreach (var d in devices.Devices)
    {
        Log.Information("Audio device: {Name}, Id={Id}, Channels={Channels}", d.Name, d.Id, d.Channels);
    }

    // Validate routing against selected device channels
    var selectedDeviceId = string.IsNullOrWhiteSpace(engineOptions.OutputDevice)
        ? devices.DefaultDeviceId
        : engineOptions.OutputDevice;
    var selected = devices.Devices.FirstOrDefault(d => string.Equals(d.Id, selectedDeviceId, StringComparison.OrdinalIgnoreCase))
        ?? devices.Devices.First();
    var routingCheck = RoutingValidator.ValidateAgainstDeviceChannels(engineOptions.Routing, selected.Channels);
    if (!routingCheck.IsValid)
    {
        foreach (var err in routingCheck.Errors)
            Log.Error("Routing ERROR: {Error}", err);
    }
}
catch (Exception ex)
{
    Log.Error(ex, "Device enumeration failed: {Message}", ex.Message);
}

// Command: DeviceList — reply with current devices
connection.On("DeviceList", async () =>
{
    try
    {
        var devices = await deviceEnumerator.EnumerateAsync();
        await connection.InvokeAsync("DeviceListResult", session, devices);
    }
    catch (Exception ex)
    {
        Log.Error(ex, "Error in DeviceList: {Message}", ex.Message);
    }
});

// Back-compat: PlayTrack command
connection.On<PlayTrack>("PlayTrack", async cmd =>
{
    try
    {
        Log.Information("PlayTrack received: {FileUrl}", cmd.FileUrl);
        metrics.SetPlaying(cmd.FileUrl);
        await engine.OnTrackPlayRequested(cmd.FileUrl);
    }
    catch (Exception ex)
    {
        Log.Error(ex, "Error attempting to play: {Message}", ex.Message);
        metrics.SetError(ex.Message);
    }
});

// New commands: TrackPlayRequested (string url) and TrackStopped ()
connection.On<string>("TrackPlayRequested", async url =>
{
    try
    {
        Log.Information("TrackPlayRequested: {Url}", url);
        metrics.SetPlaying(url);
        await engine.OnTrackPlayRequested(url);
    }
    catch (Exception ex)
    {
        Log.Error(ex, "Error in TrackPlayRequested: {Message}", ex.Message);
        metrics.SetError(ex.Message);
    }
});

connection.On("TrackStopped", async () =>
{
    try
    {
        Log.Information("TrackStopped received");
        metrics.SetStopped();
        await engine.OnTrackStopped();
    }
    catch (Exception ex)
    {
        Log.Error(ex, "Error in TrackStopped: {Message}", ex.Message);
    }
});

// Ping/Echo: respond quickly with engine timestamp
connection.On<long>("Ping", async clientTicks =>
{
    try
    {
        var now = DateTimeOffset.UtcNow;
        // Estimate RTT by doubling one-way delay (approximate)
        var clientTime = new DateTimeOffset(clientTicks, TimeSpan.Zero);
        var oneWayMs = Math.Max(0, (now - clientTime).TotalMilliseconds);
        metrics.AddRttSample(oneWayMs * 2);
        var engineTicks = now.Ticks;
        await connection.InvokeAsync("Echo", session, clientTicks, engineTicks);
    }
    catch (Exception ex)
    {
        Log.Error(ex, "Error replying to Ping: {Message}", ex.Message);
    }
});

// Heartbeat: every 5s emit current engine status (Ready|Playing)
async Task RunHeartbeatAsync(CancellationToken token)
{
    while (!token.IsCancellationRequested)
    {
        try
        {
            var status = player.IsPlaying ? EngineStatus.Playing : EngineStatus.Ready;
            var lat = (player as IHasLatency)?.OutputLatencyMs ?? 0d;
            await connection.InvokeAsync("EngineStatusChanged", session, new EngineStatusChanged(status, lat), token);
        }
        catch (TaskCanceledException) { }
        catch (Exception ex)
        {
            Log.Error(ex, "Heartbeat error: {Message}", ex.Message);
        }
        try { await Task.Delay(TimeSpan.FromSeconds(5), token); } catch { }
    }
}

try
{
    if (cloudAgent is not null)
    {
        if (!string.IsNullOrWhiteSpace(pairCode))
        {
            await cloudAgent.PairAsync(pairCode, agentName, cts.Token);
            Log.Information("Show Agent paired as {AgentName}; credential protected with Windows DPAPI", agentName);
        }
        var lease = await cloudAgent.EnsureLeaseAsync(cts.Token)
            ?? throw new InvalidOperationException("Show Agent credential is missing or revoked. Pair this computer again.");
        Log.Information("Outbound Show Agent lease established. Workspace={WorkspaceId}, Session={SessionCode}, ExpiresAt={ExpiresAt}",
            lease.WorkspaceId, lease.SessionCode, lease.ExpiresAt);
        var showSnapshot = await cloudAgent.GetSnapshotAsync(cts.Token)
            ?? throw new InvalidOperationException("This Session has no immutable Session Setlist Snapshot.");
        if (showSnapshot.WorkspaceId != lease.WorkspaceId || showSnapshot.SessionCode != lease.SessionCode)
            throw new InvalidOperationException("Show Agent received a snapshot outside its paired Session.");
        var cacheRoot = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "Nuotti", "show-cache", lease.WorkspaceId, lease.SessionCode);
        var venueCache = new VenueAssetCache(httpClient, cacheRoot);
        var cacheOverrides = GetArg(args, "cache-overrides", envVar: "NUOTTI_CACHE_OVERRIDES")
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .ToHashSet(StringComparer.Ordinal);
        var cachePreflight = await venueCache.PrepareAsync(showSnapshot, cloudAgent.GetAssetGrantAsync,
            cacheOverrides, cts.Token);
        foreach (var finding in cachePreflight.Findings)
            Log.Warning("Venue cache preflight {Code}: {Detail}", finding.Code, finding.Detail);
        if (!cachePreflight.Ready)
        {
            await cloudAgent.ReportStatusAsync("Error", string.Join(" ", cachePreflight.Findings
                .Select(x => x.Detail)), cts.Token);
            throw new InvalidOperationException("Venue cache preflight failed. Resolve or explicitly accept the named safe degradations.");
        }
        Log.Information("Venue cache ready. Snapshot={SnapshotId}, Assets={AssetCount}",
            showSnapshot.SnapshotId, cachePreflight.LocalPaths.Count);
        long cursor = cloudAgent.LoadCursor();
        var nextHeartbeat = DateTimeOffset.MinValue;
        while (!cts.IsCancellationRequested)
        {
            var commands = await cloudAgent.PollAsync(cursor, cts.Token);
            if (commands is null)
            {
                Log.Warning("Show Agent was revoked. No new commands will be accepted; current playback may finish.");
                while (player.IsPlaying && !cts.IsCancellationRequested)
                    await Task.Delay(250, cts.Token);
                break;
            }
            foreach (var command in commands)
            {
                switch (command.MessageType)
                {
                    case "Prepare":
                        Log.Information("Prepare received from cloud Backend; venue cache already verified at pair time");
                        await cloudAgent.ReportStatusAsync("Ready", "prepared", cts.Token);
                        break;
                    case "PlayTrack":
                        var play = ShowAgentCloudClient.DeserializePayload<PlayTrack>(command.Payload);
                        if (play is not null)
                        {
                            if (!VenueAssetCache.TryResolveCapturedSource(play, cachePreflight.LocalPaths, out var source))
                            {
                                await cloudAgent.ReportStatusAsync("Error",
                                    $"PlayTrack rejected: asset '{play.AssetRevisionId ?? play.FileUrl}' is not in the verified Session cache.", cts.Token);
                                throw new InvalidOperationException("Playback command referenced material outside the Session Setlist Snapshot.");
                            }
                            await engine.OnTrackPlayRequested(new Uri(source).AbsoluteUri);
                        }
                        break;
                    case "StopTrack":
                        await engine.OnTrackStopped();
                        break;
                }
                cursor = Math.Max(cursor, command.Sequence);
                cloudAgent.CommitCursor(cursor);
            }
            if (DateTimeOffset.UtcNow >= nextHeartbeat)
            {
                await cloudAgent.ReportStatusAsync(player.IsPlaying ? "Playing" : "Ready", null, cts.Token);
                nextHeartbeat = DateTimeOffset.UtcNow.AddSeconds(5);
            }
            await Task.Delay(250, cts.Token);
        }
    }
    else
    {
        // Development compatibility only. Paired Windows agents use outbound HTTPS polling above.
        await connection.StartAsync(cts.Token);
        await connection.InvokeAsync("Join", session, "engine", null, cancellationToken: cts.Token);
        var initLat = (player as IHasLatency)?.OutputLatencyMs ?? 0d;
        await connection.InvokeAsync("EngineStatusChanged", session, new EngineStatusChanged(EngineStatus.Ready, initLat), cancellationToken: cts.Token);
        _ = RunHeartbeatAsync(cts.Token);
        Log.Information("Connected and joined legacy development session. Waiting for commands... Press Ctrl+C to exit.");
        await Task.Delay(-1, cts.Token);
    }
}
catch (TaskCanceledException)
{
    // normal shutdown
}
catch (Exception ex)
{
    Log.Fatal(ex, "Fatal error: {Message}", ex.Message);
}
finally
{
    try
    {
        // Graceful shutdown: stop any playback and emit Ready before disconnecting
        try
        {
            if (player.IsPlaying)
            {
                await player.StopAsync();
            }
        }
        catch { }
        try
        {
            var shutLat = (player as IHasLatency)?.OutputLatencyMs ?? 0d;
            await connection.InvokeAsync("EngineStatusChanged", session, new EngineStatusChanged(EngineStatus.Ready, shutLat));
        }
        catch { }
    }
    finally
    {
        try { (player as IDisposable)?.Dispose(); } catch { }
        try { provider?.Dispose(); } catch { }
        try { await connection.DisposeAsync(); } catch { }
    }
    Log.Information("AudioEngine stopped.");
    Log.CloseAndFlush();
}
