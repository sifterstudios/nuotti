using Nuotti.AudioEngine.Playback;
using Nuotti.Contracts.V1.Enum;
using Nuotti.Contracts.V1.Message;
using Nuotti.Contracts.V1.Model;
namespace Nuotti.AudioEngine;

public sealed class EngineCoordinator
{
    private readonly IAudioPlayer _player;
    private readonly IEngineStatusSink _sink;
    private readonly ISourcePreflight? _preflight;
    private readonly IProblemSink? _problemSink;
    private readonly IClickSource? _click;

    public EngineCoordinator(IAudioPlayer player, IEngineStatusSink sink)
        : this(player, sink, preflight: null, problemSink: null, click: null)
    {
    }

    public EngineCoordinator(IAudioPlayer player, IEngineStatusSink sink, ISourcePreflight? preflight, IProblemSink? problemSink, IClickSource? click = null)
    {
        _player = player;
        _sink = sink;
        _preflight = preflight;
        _problemSink = problemSink;
        _click = click;

        _player.Started += async (_, __) =>
        {
            try { if (_click?.Enabled == true) _click.Start(); } catch { /* ignore click errors for now */ }
            var lat = (_player as IHasLatency)?.OutputLatencyMs ?? 0d;
            await _sink.PublishAsync(new EngineStatusChanged(EngineStatus.Playing, lat));
        };
        _player.Stopped += async (_, __) =>
        {
            try { _click?.Stop(); } catch { /* ignore */ }
            await _sink.PublishAsync(new EngineStatusChanged(EngineStatus.Ready, 0));
        };
        _player.Error += async (_, __) => await _sink.PublishAsync(new EngineStatusChanged(EngineStatus.Error, 0));
    }

    public async Task OnTrackPlayRequested(string fileUrl, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(fileUrl)) return;

        // Optional preflight/normalization
        if (_preflight is not null)
        {
            var pre = await _preflight.CheckAsync(fileUrl, cancellationToken);
            if (!pre.Ok)
            {
                // Emit problem if available
                if (pre.Problem is NuottiProblem problem && _problemSink is not null)
                {
                    try { await _problemSink.PublishAsync(problem, cancellationToken); } catch { /* ignore */ }
                }
                await _sink.PublishAsync(new EngineStatusChanged(EngineStatus.Error, 0), cancellationToken);
                return;
            }
            if (!string.IsNullOrWhiteSpace(pre.NormalizedUrl))
            {
                fileUrl = pre.NormalizedUrl!;
            }
        }

        if (_player.IsPlaying)
        {
            await _player.StopAsync(cancellationToken);
        }
        await _player.PlayAsync(fileUrl, cancellationToken);
    }

    public Task OnTrackStopped(CancellationToken cancellationToken = default)
    {
        return _player.StopAsync(cancellationToken);
    }
}
