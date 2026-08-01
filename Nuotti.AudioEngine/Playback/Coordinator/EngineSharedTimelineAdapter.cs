namespace Nuotti.AudioEngine.Playback.Coordinator;

/// <summary>
/// Adapts the existing <see cref="IAudioPlayer"/> into the shared-timeline port used by the
/// stage-grade coordinator. Measured start is reported immediately after <see cref="IAudioPlayer.PlayAsync"/>
/// begins (PortAudio/ASIO first callback seam lands here when the ASIO backend replaces PortAudio).
/// </summary>
public sealed class EngineSharedTimelineAdapter(IAudioPlayer player) : ISharedTimelineAudio
{
    string? _playUrl;

    public bool IsPrimed { get; private set; }
    public bool IsRunning => player.IsPlaying;
    public long FramePosition { get; private set; }
    public long BackingOffsetFrames { get; private set; }
    public long ClickOffsetFrames => 0;

    public void Prime(VerifiedPlaybackAssets assets)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(assets.BackingPath);
        _playUrl = new Uri(assets.BackingPath).AbsoluteUri;
        BackingOffsetFrames = assets.BackingOffsetFrames;
        IsPrimed = true;
        FramePosition = 0;
    }

    public void ScheduleStart(TimeSpan plannedLead)
    {
        if (!IsPrimed) throw new InvalidOperationException("Prepare must prime assets before Start.");
        _ = plannedLead;
    }

    public TimeSpan ReportFirstCallback()
    {
        if (_playUrl is null) throw new InvalidOperationException("Not primed.");
        // Fire-and-forget play; first-callback measurement is the call itself until ASIO lands.
        _ = player.PlayAsync(_playUrl);
        FramePosition = 0;
        return TimeSpan.Zero;
    }

    public void Stop() => _ = player.StopAsync();

    public void InjectUnderrun() => Stop();

    public void InjectDriverLoss() => Stop();
}
