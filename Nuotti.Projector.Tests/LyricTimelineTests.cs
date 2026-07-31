using FluentAssertions;
using Nuotti.Projector.Presentation.Playback;
using Xunit;

namespace Nuotti.Projector.Tests;

public sealed class LyricTimelineTests
{
    const string Lrc = """
        [00:00.00]Count-in is still running
        [00:01.50]First sung line
        [00:04.00]Second sung line
        """;

    [Fact]
    public void Song_start_offset_delays_LRC_activation_without_rewriting_the_track()
    {
        var timeline = LyricTimeline.Parse(Lrc);
        var songStartOffset = TimeSpan.FromSeconds(2);

        timeline.ActiveLineAt(TimeSpan.FromMilliseconds(1_999), songStartOffset).Should().BeNull();
        timeline.ActiveLineAt(TimeSpan.FromSeconds(2), songStartOffset)!.Text.Should().Be("Count-in is still running");
        timeline.ActiveLineAt(TimeSpan.FromMilliseconds(3_500), songStartOffset)!.Text.Should().Be("First sung line");
    }

    [Fact]
    public void Active_line_changes_from_local_time_without_per_line_messages()
    {
        var timeline = LyricTimeline.Parse(Lrc);

        timeline.ActiveLineAt(TimeSpan.FromMilliseconds(3_999), TimeSpan.Zero)!.Text
            .Should().Be("First sung line");
        timeline.ActiveLineAt(TimeSpan.FromSeconds(4), TimeSpan.Zero)!.Text
            .Should().Be("Second sung line");
    }
}
