using Nuotti.AudioEngine.Playback;
using Nuotti.Contracts.V1.Message;
namespace Nuotti.AudioEngine;

public sealed class PlayCommandHandler
{
    private readonly IAudioPlayer _player;

    public PlayCommandHandler(IAudioPlayer player)
    {
        _player = player;
    }

    public Task HandleAsync(PlayTrack cmd, CancellationToken cancellationToken = default)
    {
        return _player.PlayAsync(cmd.FileUrl, cancellationToken);
    }
}