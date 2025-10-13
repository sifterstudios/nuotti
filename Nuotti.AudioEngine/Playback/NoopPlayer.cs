namespace Nuotti.AudioEngine.Playback;

public sealed class NoopPlayer : IAudioPlayer
{
    public event EventHandler? Started;
    public event EventHandler<bool>? Stopped;
    public event EventHandler<Exception>? Error;

    public bool IsPlaying { get; private set; }

    public Task PlayAsync(string url, CancellationToken cancellationToken = default)
    {
        if (IsPlaying)
            return Task.CompletedTask;

        IsPlaying = true;
        Started?.Invoke(this, EventArgs.Empty);
        // Complete immediately; simulate short playback but still considered playing until StopAsync
        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken = default)
    {
        if (!IsPlaying) return Task.CompletedTask;
        IsPlaying = false;
        Stopped?.Invoke(this, true);
        return Task.CompletedTask;
    }
}