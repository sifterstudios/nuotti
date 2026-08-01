using FluentAssertions;
using Nuotti.Projector.Presentation.Playback;
using Xunit;

namespace Nuotti.Projector.Tests;

public sealed class ProjectorPlaybackPresenterTests
{
    static PlaybackAnchor Anchor(
        long sequence = 1,
        long frame = 0,
        double rate = 1,
        PlaybackAnchorState state = PlaybackAnchorState.Playing)
        => new(
            "play-1",
            "rev-1",
            SampleRate: 48_000,
            Frame: frame,
            EngineMonotonicTimestamp: TimeSpan.FromMilliseconds(frame / 48.0),
            BackendUtcCorrelation: DateTimeOffset.Parse("2026-08-01T12:00:00Z"),
            state,
            rate,
            sequence,
            ControlGeneration: 1);

    [Fact]
    public void Activates_unchanged_LRC_using_songStartOffset_and_emits_no_audio()
    {
        var presenter = new ProjectorPlaybackPresenter();
        presenter.LoadLyrics(
            "[00:00.00]Count-in\n[00:01.50]First line",
            TimeSpan.FromSeconds(2));
        presenter.ApplyAnchor(Anchor(frame: 0), TimeSpan.Zero, DateTimeOffset.Parse("2026-08-01T12:00:00Z"));

        var before = presenter.Present(TimeSpan.FromMilliseconds(1_999));
        before.EmitsAudio.Should().BeFalse();
        before.ActiveLine.Should().BeNull();

        var after = presenter.Present(TimeSpan.FromMilliseconds(3_500));
        after.EmitsAudio.Should().BeFalse();
        after.ActiveLine!.Text.Should().Be("First line");
        after.Connection.Should().Be(ProjectorConnectionVisual.Live);
    }

    [Fact]
    public void Disconnect_freezes_safe_visual_then_reconcile_snaps_to_new_anchor()
    {
        var presenter = new ProjectorPlaybackPresenter();
        presenter.LoadLyrics("[00:00.00]Hold\n[00:05.00]After", TimeSpan.Zero);
        presenter.ApplyAnchor(Anchor(frame: 0), TimeSpan.Zero, DateTimeOffset.Parse("2026-08-01T12:00:00Z"));

        var live = presenter.Present(TimeSpan.FromSeconds(1));
        live.ActiveLine!.Text.Should().Be("Hold");

        presenter.Freeze(TimeSpan.FromSeconds(1));
        var frozen = presenter.Present(TimeSpan.FromSeconds(10));
        frozen.Connection.Should().Be(ProjectorConnectionVisual.Holding);
        frozen.ActiveLine!.Text.Should().Be("Hold");
        frozen.Position.Should().Be(live.Position);

        var receivedUtc = DateTimeOffset.Parse("2026-08-01T12:00:05Z");
        presenter.Reconcile(Anchor(sequence: 2, frame: 48_000 * 5), TimeSpan.FromSeconds(10), receivedUtc);
        var resumed = presenter.Present(TimeSpan.FromSeconds(10));
        resumed.Connection.Should().Be(ProjectorConnectionVisual.Live);
        resumed.ActiveLine!.Text.Should().Be("After");
    }

    [Fact]
    public void Without_lyrics_holding_pattern_is_neutral_fallback()
    {
        var presenter = new ProjectorPlaybackPresenter();
        presenter.Freeze(TimeSpan.Zero);
        var frame = presenter.Present(TimeSpan.FromSeconds(1));
        frame.Connection.Should().Be(ProjectorConnectionVisual.Holding);
        frame.ActiveLine.Should().BeNull();
        frame.FallbackMessage.Should().Be("Reconnecting…");
        frame.EmitsAudio.Should().BeFalse();
    }
}
