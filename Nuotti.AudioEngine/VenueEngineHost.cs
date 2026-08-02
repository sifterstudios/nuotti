using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Nuotti.AudioEngine.Output;
using Nuotti.AudioEngine.Playback;
using Nuotti.Contracts.V1.Enum;
using Nuotti.Contracts.V1.Message;
using Serilog;

namespace Nuotti.AudioEngine;

/// <summary>
/// In-process Show Agent / audio engine for the Venue app: one hub connection as Engine,
/// driven by the same access-token provider the Projector already paired.
/// </summary>
public sealed class VenueEngineHost : IAsyncDisposable
{
    readonly IVenueEngineTransport _transport;
    readonly IAudioPlayer _player;
    readonly EngineCoordinator _engine;
    readonly IServiceProvider? _ownedServices;
    readonly CancellationTokenSource _lifetime = new();
    Task? _heartbeat;
    bool _started;

    public bool IsStarted => _started;

    /// <summary>Hub URL for the Engine role (explicit deviceRole so it mirrors the Projector connection).</summary>
    public static string BuildHubUrl(string backendBaseUrl)
        => $"{backendBaseUrl.TrimEnd('/')}/hub?deviceRole=engine";

    /// <summary>Production constructor: builds PortAudio/SystemPlayer backends and a SignalR transport.</summary>
    public VenueEngineHost(
        string backendBaseUrl,
        string sessionCode,
        Func<Task<string?>> accessTokenProvider)
        : this(
            new SignalRVenueEngineTransport(backendBaseUrl, sessionCode, accessTokenProvider),
            CreateDefaultPlayer(out var services, out var http),
            ownedServices: services,
            preflight: new HttpFilePreflight(http))
    {
    }

    /// <summary>Test seam: inject transport and player.</summary>
    public VenueEngineHost(
        IVenueEngineTransport transport,
        IAudioPlayer player,
        IServiceProvider? ownedServices = null,
        IEngineStatusSink? statusSink = null,
        ISourcePreflight? preflight = null,
        IProblemSink? problemSink = null)
    {
        _transport = transport;
        _player = player;
        _ownedServices = ownedServices;
        _engine = new EngineCoordinator(
            player,
            statusSink ?? new TransportStatusSink(transport),
            preflight,
            problemSink ?? new NoopProblemSink());
    }

    public async Task StartAsync(CancellationToken cancellationToken = default)
    {
        if (_started) return;

        _transport.OnPlayTrack(async cmd =>
        {
            try { await _engine.OnTrackPlayRequested(cmd.FileUrl, cancellationToken); }
            catch (Exception ex) { Log.Error(ex, "Venue engine PlayTrack failed"); }
        });
        _transport.OnTrackPlayRequested(async url =>
        {
            try { await _engine.OnTrackPlayRequested(url, cancellationToken); }
            catch (Exception ex) { Log.Error(ex, "Venue engine TrackPlayRequested failed"); }
        });
        _transport.OnTrackStopped(async () =>
        {
            try { await _engine.OnTrackStopped(cancellationToken); }
            catch (Exception ex) { Log.Error(ex, "Venue engine TrackStopped failed"); }
        });

        await _transport.ConnectAsync(cancellationToken);
        var lat = (_player as IHasLatency)?.OutputLatencyMs ?? 0d;
        await _transport.ReportStatusAsync(new EngineStatusChanged(EngineStatus.Ready, lat), cancellationToken);
        _heartbeat = RunHeartbeatAsync(_lifetime.Token);
        _started = true;
        Log.Information("Venue engine host connected as Engine");
    }

    public async Task StopAsync()
    {
        if (!_started && _heartbeat is null) return;
        _lifetime.Cancel();
        try
        {
            if (_player.IsPlaying) await _player.StopAsync();
        }
        catch { /* best effort */ }

        try
        {
            var lat = (_player as IHasLatency)?.OutputLatencyMs ?? 0d;
            await _transport.ReportStatusAsync(new EngineStatusChanged(EngineStatus.Ready, lat));
        }
        catch { /* best effort */ }

        if (_heartbeat is not null)
        {
            try { await _heartbeat; }
            catch (OperationCanceledException) { }
        }

        await _transport.DisposeAsync();
        _started = false;
    }

    public async ValueTask DisposeAsync()
    {
        await StopAsync();
        try { (_player as IDisposable)?.Dispose(); }
        catch { }
        if (_ownedServices is IDisposable d) d.Dispose();
        _lifetime.Dispose();
    }

    async Task RunHeartbeatAsync(CancellationToken token)
    {
        while (!token.IsCancellationRequested)
        {
            try
            {
                var status = _player.IsPlaying ? EngineStatus.Playing : EngineStatus.Ready;
                var lat = (_player as IHasLatency)?.OutputLatencyMs ?? 0d;
                await _transport.ReportStatusAsync(new EngineStatusChanged(status, lat), token);
            }
            catch (OperationCanceledException) { break; }
            catch (Exception ex) { Log.Error(ex, "Venue engine heartbeat failed"); }

            try { await Task.Delay(TimeSpan.FromSeconds(5), token); }
            catch (OperationCanceledException) { break; }
        }
    }

    static IAudioPlayer CreateDefaultPlayer(out ServiceProvider services, out HttpClient http)
    {
        var configuration = new ConfigurationBuilder()
            .SetBasePath(AppContext.BaseDirectory)
            .AddJsonFile("engine.json", optional: true, reloadOnChange: false)
            .AddEnvironmentVariables(prefix: "NUOTTI_ENGINE__")
            .Build();
        var collection = new ServiceCollection();
        collection.AddAudioBackends(configuration);
        services = collection.BuildServiceProvider();
        var opts = services.GetRequiredService<IOptions<EngineOptions>>().Value;
        try { opts.Validate(); }
        catch (ArgumentException ex)
        {
            Log.Warning(ex, "Venue engine options invalid; continuing with defaults where possible");
        }
        http = new HttpClient();
        return services.GetRequiredService<IAudioBackend>().CreatePlayer(opts);
    }

    sealed class TransportStatusSink(IVenueEngineTransport transport) : IEngineStatusSink
    {
        public Task PublishAsync(EngineStatusChanged evt, CancellationToken cancellationToken = default)
            => transport.ReportStatusAsync(evt, cancellationToken);
    }

    sealed class NoopProblemSink : IProblemSink
    {
        public Task PublishAsync(Nuotti.Contracts.V1.Model.NuottiProblem problem, CancellationToken cancellationToken = default)
            => Task.CompletedTask;
    }
}
