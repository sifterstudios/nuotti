using FluentAssertions;
using Nuotti.Projector.Presentation.Playback;
using Xunit;

namespace Nuotti.Projector.Tests;

public sealed class PlaybackTimelineSynchronizerTests
{
    static readonly DateTimeOffset Epoch = new(2026, 7, 31, 12, 0, 0, TimeSpan.Zero);

    static PlaybackAnchor Anchor(double positionMs, double observedAtMs, long sequence = 1)
        => new(
            PlaybackInstanceId: "playback-1",
            SongPackageRevisionId: "song-revision-1",
            SampleRate: 48_000,
            Frame: (long)(positionMs * 48),
            EngineMonotonicTimestamp: TimeSpan.FromMilliseconds(observedAtMs),
            BackendUtcCorrelation: Epoch + TimeSpan.FromMilliseconds(observedAtMs),
            State: PlaybackAnchorState.Playing,
            Rate: 1,
            Sequence: sequence,
            ControlGeneration: 1);

    [Fact]
    public void Position_advances_locally_between_general_playback_anchors()
    {
        var timeline = new PlaybackTimelineSynchronizer();

        timeline.ApplyAnchor(Anchor(positionMs: 0, observedAtMs: 0), TimeSpan.Zero, Epoch);

        timeline.PositionAt(TimeSpan.FromSeconds(2)).Should().Be(TimeSpan.FromSeconds(2));
    }

    [Theory]
    [InlineData(40, DriftCorrection.Ignore)]
    [InlineData(100, DriftCorrection.Gradual)]
    [InlineData(200, DriftCorrection.Snap)]
    public void Anchor_error_selects_the_specified_correction(double errorMs, DriftCorrection expected)
    {
        var timeline = new PlaybackTimelineSynchronizer();
        timeline.ApplyAnchor(Anchor(0, 0), TimeSpan.Zero, Epoch);

        var result = timeline.ApplyAnchor(Anchor(2_000 + errorMs, 2_000, 2), TimeSpan.FromSeconds(2), Epoch + TimeSpan.FromSeconds(2));

        result.Correction.Should().Be(expected);
        result.Error.Should().BeCloseTo(TimeSpan.FromMilliseconds(errorMs), TimeSpan.FromTicks(1));
    }

    [Fact]
    public void Gradual_correction_converges_without_a_visual_jump()
    {
        var timeline = new PlaybackTimelineSynchronizer();
        timeline.ApplyAnchor(Anchor(0, 0), TimeSpan.Zero, Epoch);
        timeline.ApplyAnchor(Anchor(2_100, 2_000, 2), TimeSpan.FromSeconds(2), Epoch + TimeSpan.FromSeconds(2));

        timeline.PositionAt(TimeSpan.FromSeconds(2)).Should().Be(TimeSpan.FromSeconds(2));
        timeline.PositionAt(TimeSpan.FromMilliseconds(2_500)).Should().Be(TimeSpan.FromMilliseconds(2_550));
        timeline.PositionAt(TimeSpan.FromSeconds(3)).Should().Be(TimeSpan.FromMilliseconds(3_100));
    }

    [Fact]
    public void Snap_replaces_the_local_position_immediately()
    {
        var timeline = new PlaybackTimelineSynchronizer();
        timeline.ApplyAnchor(Anchor(0, 0), TimeSpan.Zero, Epoch);

        timeline.ApplyAnchor(Anchor(2_200, 2_000, 2), TimeSpan.FromSeconds(2), Epoch + TimeSpan.FromSeconds(2));

        timeline.PositionAt(TimeSpan.FromSeconds(2)).Should().Be(TimeSpan.FromMilliseconds(2_200));
    }
}
