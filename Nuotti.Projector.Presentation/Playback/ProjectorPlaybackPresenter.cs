namespace Nuotti.Projector.Presentation.Playback;

public enum ProjectorConnectionVisual
{
    Live,
    Holding
}

public sealed record ProjectorPlaybackFrame(
    TimeSpan Position,
    LyricLine? ActiveLine,
    ProjectorConnectionVisual Connection,
    bool EmitsAudio,
    string? FallbackMessage = null);

/// <summary>
/// Composes sparse playback anchors with unchanged LRC. The Projector never emits audio;
/// lyrics activate using the package <c>songStartOffset</c> only.
/// </summary>
public sealed class ProjectorPlaybackPresenter
{
    readonly PlaybackTimelineSynchronizer _sync = new();
    LyricTimeline? _lyrics;
    TimeSpan _songStartOffset;
    bool _frozen;
    TimeSpan _frozenPosition;

    public bool EmitsAudio => false;

    public void LoadLyrics(string lrc, TimeSpan songStartOffset)
    {
        _lyrics = LyricTimeline.Parse(lrc);
        _songStartOffset = songStartOffset;
    }

    public AnchorApplication ApplyAnchor(PlaybackAnchor anchor, TimeSpan receivedAt, DateTimeOffset receivedUtc)
        => _sync.ApplyAnchor(anchor, receivedAt, receivedUtc);

    public void Freeze(TimeSpan localTime)
    {
        _frozenPosition = _sync.PositionAt(localTime);
        _frozen = true;
    }

    public AnchorApplication Reconcile(PlaybackAnchor anchor, TimeSpan receivedAt, DateTimeOffset receivedUtc)
    {
        _frozen = false;
        return _sync.ApplyAnchor(anchor, receivedAt, receivedUtc);
    }

    /// <summary>
    /// Clears the holding pattern after reconnect when no fresh anchor has arrived yet.
    /// Continues from the frozen lyric position on the local clock.
    /// </summary>
    public void ResumeFromHold(TimeSpan localTime)
    {
        if (!_frozen) return;
        _frozen = false;
        _sync.ForcePosition(_frozenPosition, localTime);
    }

    public ProjectorPlaybackFrame Present(TimeSpan localTime)
    {
        if (_frozen)
        {
            var heldLine = _lyrics?.ActiveLineAt(_frozenPosition, _songStartOffset);
            return new(
                _frozenPosition,
                heldLine,
                ProjectorConnectionVisual.Holding,
                EmitsAudio: false,
                FallbackMessage: heldLine is null ? "Reconnecting…" : null);
        }

        var position = _sync.PositionAt(localTime);
        return new(
            position,
            _lyrics?.ActiveLineAt(position, _songStartOffset),
            ProjectorConnectionVisual.Live,
            EmitsAudio: false);
    }
}
