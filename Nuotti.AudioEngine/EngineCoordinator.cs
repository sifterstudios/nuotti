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

    public EngineCoordinator(IAudioPlayer player, IEngineStatusSink sink)
        : this(player, sink, preflight: null, problemSink: null)
    {
    }

    public EngineCoordinator(IAudioPlayer player, IEngineStatusSink sink, ISourcePreflight? preflight, IProblemSink? problemSink)
    {
        _player = player;
        _sink = sink;
        _preflight = preflight;
        _problemSink = problemSink;

        _player.Started += async (_, __) => await _sink.PublishAsync(new EngineStatusChanged(EngineStatus.Playing));
        _player.Stopped += async (_, __) => await _sink.PublishAsync(new EngineStatusChanged(EngineStatus.Ready));
        _player.Error += async (_, __) => await _sink.PublishAsync(new EngineStatusChanged(EngineStatus.Error));
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
                await _sink.PublishAsync(new EngineStatusChanged(EngineStatus.Error), cancellationToken);
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
