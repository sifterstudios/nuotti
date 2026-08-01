namespace Nuotti.Projector.Presentation.Playback;

public sealed record PlaybackAnchor(
    string PlaybackInstanceId,
    string SongPackageRevisionId,
    int SampleRate,
    long Frame,
    TimeSpan EngineMonotonicTimestamp,
    DateTimeOffset BackendUtcCorrelation,
    PlaybackAnchorState State,
    double Rate,
    long Sequence,
    long ControlGeneration);

public enum PlaybackAnchorState
{
    Scheduled,
    Playing,
    Completed
}

public enum DriftCorrection
{
    Ignore,
    Gradual,
    Snap
}

public sealed record AnchorApplication(DriftCorrection Correction, TimeSpan Error);

/// <summary>
/// Projects sparse Playback anchors onto a local monotonic clock. The Projector can render every
/// frame from this timeline; it does not need a network message for every visual transition.
/// </summary>
public sealed class PlaybackTimelineSynchronizer
{
    static readonly TimeSpan IgnoreThreshold = TimeSpan.FromMilliseconds(50);
    static readonly TimeSpan SnapThreshold = TimeSpan.FromMilliseconds(150);
    static readonly TimeSpan ConvergenceWindow = TimeSpan.FromSeconds(1);

    bool _hasAnchor;
    string? _playbackInstanceId;
    long _controlGeneration;
    long _sequence;
    TimeSpan _basePosition;
    TimeSpan _baseLocalTime;
    double _rate;
    TimeSpan _pendingCorrection;
    TimeSpan _correctionStartedAt;

    public AnchorApplication ApplyAnchor(PlaybackAnchor anchor, TimeSpan receivedAt, DateTimeOffset receivedUtc)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(anchor.SampleRate, 1);
        ArgumentOutOfRangeException.ThrowIfNegative(anchor.Frame);
        ArgumentOutOfRangeException.ThrowIfNegative(anchor.Rate);

        var authoritativePosition = TimeSpan.FromSeconds((double)anchor.Frame / anchor.SampleRate)
            + Scale(NonNegative(receivedUtc - anchor.BackendUtcCorrelation), anchor.Rate);

        if (!_hasAnchor
            || anchor.PlaybackInstanceId != _playbackInstanceId
            || anchor.ControlGeneration != _controlGeneration)
        {
            SnapTo(anchor, receivedAt, authoritativePosition);
            return new AnchorApplication(DriftCorrection.Snap, TimeSpan.Zero);
        }

        if (anchor.Sequence <= _sequence)
        {
            return new AnchorApplication(DriftCorrection.Ignore, TimeSpan.Zero);
        }

        var predictedPosition = PositionAt(receivedAt);
        var error = authoritativePosition - predictedPosition;
        _sequence = anchor.Sequence;

        if (error.Duration() <= IgnoreThreshold)
        {
            return new AnchorApplication(DriftCorrection.Ignore, error);
        }

        if (error.Duration() <= SnapThreshold)
        {
            _basePosition = predictedPosition;
            _baseLocalTime = receivedAt;
            _rate = anchor.Rate;
            _pendingCorrection = error;
            _correctionStartedAt = receivedAt;
            return new AnchorApplication(DriftCorrection.Gradual, error);
        }

        SnapTo(anchor, receivedAt, authoritativePosition);
        return new AnchorApplication(DriftCorrection.Snap, error);
    }

    public TimeSpan PositionAt(TimeSpan localTime)
    {
        if (!_hasAnchor)
        {
            return TimeSpan.Zero;
        }

        var elapsed = NonNegative(localTime - _baseLocalTime);
        var correctionProgress = Math.Clamp(
            (localTime - _correctionStartedAt).TotalMilliseconds / ConvergenceWindow.TotalMilliseconds,
            0,
            1);

        return _basePosition
            + Scale(elapsed, _rate)
            + Scale(_pendingCorrection, correctionProgress);
    }

    /// <summary>
    /// Re-bases the local clock at a known position after a hold, without requiring a new network anchor.
    /// </summary>
    public void ForcePosition(TimeSpan position, TimeSpan localTime, double rate = 1)
    {
        _hasAnchor = true;
        _basePosition = position;
        _baseLocalTime = localTime;
        _rate = rate;
        _pendingCorrection = TimeSpan.Zero;
        _correctionStartedAt = localTime;
    }

    void SnapTo(PlaybackAnchor anchor, TimeSpan receivedAt, TimeSpan position)
    {
        _hasAnchor = true;
        _playbackInstanceId = anchor.PlaybackInstanceId;
        _controlGeneration = anchor.ControlGeneration;
        _sequence = anchor.Sequence;
        _basePosition = position;
        _baseLocalTime = receivedAt;
        _rate = anchor.Rate;
        _pendingCorrection = TimeSpan.Zero;
        _correctionStartedAt = receivedAt;
    }

    static TimeSpan NonNegative(TimeSpan value) => value < TimeSpan.Zero ? TimeSpan.Zero : value;

    static TimeSpan Scale(TimeSpan value, double factor) => TimeSpan.FromTicks((long)(value.Ticks * factor));
}
