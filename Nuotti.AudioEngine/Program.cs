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

static void Log(string message)
{
    Console.WriteLine($"[{DateTime.Now:HH:mm:ss}] {message}");
}


// Load engine options from engine.json and environment (NUOTTI_ENGINE__*)
var configuration = new ConfigurationBuilder()
    .SetBasePath(AppContext.BaseDirectory)
    .AddJsonFile("engine.json", optional: true, reloadOnChange: false)
    .AddEnvironmentVariables(prefix: "NUOTTI_ENGINE__")
    .Build();
var engineOptions = new EngineOptions();
configuration.Bind(engineOptions);
engineOptions.Validate();
Log("Engine effective config:\n" + JsonSerializer.Serialize(engineOptions, new JsonSerializerOptions { WriteIndented = true }));

var backend = GetArg(args, "backend", envVar: "NUOTTI_BACKEND", fallback: "http://localhost:5240");
var session = GetArg(args, "session", envVar: "NUOTTI_SESSION", fallback: "dev");

Log($"AudioEngine starting. Backend={backend}, Session={session}");

var cts = new CancellationTokenSource();
Console.CancelKeyPress += (_, e) => { e.Cancel = true; cts.Cancel(); };

// Setup DI for audio backends and create the audio player based on config
var services = new ServiceCollection();
services.AddAudioBackends(configuration);
var provider = services.BuildServiceProvider();
var optsFromDi = provider.GetRequiredService<IOptions<EngineOptions>>().Value;
var audioBackend = provider.GetRequiredService<IAudioBackend>();
IAudioPlayer player = audioBackend.CreatePlayer(optsFromDi);
player.Started += (_, __) => Log("Playback started.");
player.Stopped += (_, cancelled) => Log($"Playback stopped. Cancelled={cancelled}");
player.Error += (_, ex) => Log($"Playback error: {ex.Message}");

var connection = new HubConnectionBuilder()
    .WithUrl(new Uri(new Uri(backend), "/hub"))
    .WithAutomaticReconnect()
    .Build();

// Status sink that publishes to backend hub
IEngineStatusSink sink = new HubStatusSink(connection, session);
IProblemSink problemSink = new HubProblemSink(connection, session);
var httpClient = new HttpClient();
ISourcePreflight preflight = new HttpFilePreflight(httpClient);
var engine = new EngineCoordinator(player, sink, preflight, problemSink);

// Audio device enumeration (foundation)
IAudioDeviceEnumerator deviceEnumerator = new BasicAudioDeviceEnumerator();

// Log devices on startup
try
{
    var devices = await deviceEnumerator.EnumerateAsync(cts.Token);
    Log($"Audio devices (default={devices.DefaultDeviceId}):");
    foreach (var d in devices.Devices)
    {
        Log($" - {d.Name} (Id={d.Id}, Channels={d.Channels})");
    }
}
catch (Exception ex)
{
    Log($"Device enumeration failed: {ex.Message}");
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
        Log($"Error in DeviceList: {ex.Message}");
    }
});

// Back-compat: PlayTrack command
connection.On<PlayTrack>("PlayTrack", async cmd =>
{
    try
    {
        Log($"PlayTrack received: {cmd.FileUrl}");
        await engine.OnTrackPlayRequested(cmd.FileUrl);
    }
    catch (Exception ex)
    {
        Log($"Error attempting to play: {ex.Message}");
    }
});

// New commands: TrackPlayRequested (string url) and TrackStopped ()
connection.On<string>("TrackPlayRequested", async url =>
{
    try
    {
        Log($"TrackPlayRequested: {url}");
        await engine.OnTrackPlayRequested(url);
    }
    catch (Exception ex)
    {
        Log($"Error in TrackPlayRequested: {ex.Message}");
    }
});

connection.On("TrackStopped", async () =>
{
    try
    {
        Log("TrackStopped received");
        await engine.OnTrackStopped();
    }
    catch (Exception ex)
    {
        Log($"Error in TrackStopped: {ex.Message}");
    }
});

// Ping/Echo: respond quickly with engine timestamp
connection.On<long>("Ping", async clientTicks =>
{
    try
    {
        var engineTicks = DateTimeOffset.UtcNow.Ticks;
        await connection.InvokeAsync("Echo", session, clientTicks, engineTicks);
    }
    catch (Exception ex)
    {
        Log($"Error replying to Ping: {ex.Message}");
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
            await connection.InvokeAsync("EngineStatusChanged", session, new EngineStatusChanged(status), token);
        }
        catch (TaskCanceledException) { }
        catch (Exception ex)
        {
            Log($"Heartbeat error: {ex.Message}");
        }
        try { await Task.Delay(TimeSpan.FromSeconds(5), token); } catch { }
    }
}

try
{
    await connection.StartAsync(cts.Token);
    await connection.InvokeAsync("Join", session, "engine", null, cancellationToken: cts.Token);
    // Emit initial status: Ready
    await connection.InvokeAsync("EngineStatusChanged", session, new EngineStatusChanged(EngineStatus.Ready), cancellationToken: cts.Token);
    _ = RunHeartbeatAsync(cts.Token);
    Log("Connected and joined session. Waiting for PlayTrack commands... Press Ctrl+C to exit.");
    await Task.Delay(-1, cts.Token);
}
catch (TaskCanceledException)
{
    // normal shutdown
}
catch (Exception ex)
{
    Log($"Fatal: {ex.Message}");
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
            await connection.InvokeAsync("EngineStatusChanged", session, new EngineStatusChanged(EngineStatus.Ready));
        }
        catch { }
    }
    finally
    {
        try { (player as IDisposable)?.Dispose(); } catch { }
        try { provider?.Dispose(); } catch { }
        try { await connection.DisposeAsync(); } catch { }
    }
    Log("AudioEngine stopped.");
}