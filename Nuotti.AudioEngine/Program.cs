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
using Microsoft.Extensions.Hosting;
using ServiceDefaults;

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

// Configure structured logging for console app with file sink enabled
Microsoft.Extensions.Hosting.LoggingExtensions.ConfigureStructuredLogging("Nuotti.AudioEngine", configuration, enableFileSink: true);

var engineOptions = new EngineOptions();
configuration.Bind(engineOptions);
engineOptions.Validate();
Log.Information("Engine effective config: {Config}", JsonSerializer.Serialize(engineOptions, new JsonSerializerOptions { WriteIndented = true }));

// Metrics setup
var metrics = new AudioEngineMetrics();
_ = MetricsHost.RunIfEnabledAsync(engineOptions.Metrics, metrics, CancellationToken.None);

var backend = GetArg(args, "backend", envVar: "NUOTTI_BACKEND", fallback: "http://localhost:5240");
var session = GetArg(args, "session", envVar: "NUOTTI_SESSION", fallback: "dev");

var versionInfo = ServiceDefaults.VersionInfo.GetVersionInfo("Nuotti.AudioEngine");
Log.Information("AudioEngine starting. Service={Service}, Version={Version}, GitCommit={GitCommit}, BuildTime={BuildTime}, Runtime={Runtime}, Backend={Backend}, Session={Session}", 
    versionInfo.Service, versionInfo.Version, versionInfo.GitCommit, versionInfo.BuildTime, versionInfo.Runtime, backend, session);

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

// Status sink that publishes to backend hub
IEngineStatusSink sink = new HubStatusSink(connection, session);
IProblemSink problemSink = new HubProblemSink(connection, session);
var httpClient = new HttpClient();
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
    await connection.StartAsync(cts.Token);
    await connection.InvokeAsync("Join", session, "engine", null, cancellationToken: cts.Token);
    // Emit initial status: Ready
    var initLat = (player as IHasLatency)?.OutputLatencyMs ?? 0d;
    await connection.InvokeAsync("EngineStatusChanged", session, new EngineStatusChanged(EngineStatus.Ready, initLat), cancellationToken: cts.Token);
    _ = RunHeartbeatAsync(cts.Token);
    Log.Information("Connected and joined session. Waiting for PlayTrack commands... Press Ctrl+C to exit.");
    await Task.Delay(-1, cts.Token);
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