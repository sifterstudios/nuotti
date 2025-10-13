namespace Nuotti.AudioEngine.Playback;

public interface IAudioPlayer
{
    // Fires when playback successfully starts.
    event EventHandler? Started;
    // Fires when playback stops (natural finish or StopAsync). Provides whether it was cancelled.
    event EventHandler<bool>? Stopped;
    // Fires when an unrecoverable error happens during start or playback.
    event EventHandler<Exception>? Error;

    bool IsPlaying { get; }

    Task PlayAsync(string url, CancellationToken cancellationToken = default);
    Task StopAsync(CancellationToken cancellationToken = default);
}