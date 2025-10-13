using Nuotti.AudioEngine.Playback;
using Nuotti.Contracts.V1.Enum;
using Nuotti.Contracts.V1.Message;
namespace Nuotti.AudioEngine;

public sealed class EngineCoordinator
{
    private readonly IAudioPlayer _player;
    private readonly IEngineStatusSink _sink;

    public EngineCoordinator(IAudioPlayer player, IEngineStatusSink sink)
    {
        _player = player;
        _sink = sink;

        _player.Started += async (_, __) => await _sink.PublishAsync(new EngineStatusChanged(EngineStatus.Playing));
        _player.Stopped += async (_, __) => await _sink.PublishAsync(new EngineStatusChanged(EngineStatus.Ready));
        _player.Error += async (_, __) => await _sink.PublishAsync(new EngineStatusChanged(EngineStatus.Error));
    }

    public async Task OnTrackPlayRequested(string fileUrl, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(fileUrl)) return;
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
