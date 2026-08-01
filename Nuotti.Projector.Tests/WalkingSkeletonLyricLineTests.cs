using Nuotti.Projector.Presentation.Playback;
using Xunit;

namespace Nuotti.Projector.Tests;

public sealed class WalkingSkeletonLyricLineTests
{
    [Fact]
    public void Captured_session_lrc_yields_one_active_line_after_start_offset()
    {
        const string lrc = "[00:00.00]First line of the reveal\n[00:05.00]Second line";
        var line = LyricTimeline.Parse(lrc).ActiveLineAt(TimeSpan.FromSeconds(1), TimeSpan.Zero);
        Assert.Equal("First line of the reveal", line?.Text);
    }
}
