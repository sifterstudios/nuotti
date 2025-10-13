using FluentAssertions;
using Nuotti.AudioEngine.Playback;
using System;
using Xunit;
namespace Nuotti.AudioEngine.Tests;

public class ClickSourceTests
{
    [Theory]
    [InlineData(120, 500)]
    [InlineData(60, 1000)]
    [InlineData(30, 2000)]
    public void IntervalFromBpm_is_correct(int bpm, int expectedMs)
    {
        var ts = NaiveClickSource.IntervalFromBpm(bpm);
        ts.Should().Be(TimeSpan.FromMilliseconds(expectedMs));
    }

    [Fact]
    public void Enabled_false_when_level_zero()
    {
        var opts = new ClickOptions { Level = 0.0, Bpm = 120 };
        var click = new NaiveClickSource(opts, hasRouting: true);
        click.Enabled.Should().BeFalse();
    }

    [Fact]
    public void Enabled_false_when_no_routing()
    {
        var opts = new ClickOptions { Level = 0.5, Bpm = 120 };
        var click = new NaiveClickSource(opts, hasRouting: false);
        click.Enabled.Should().BeFalse();
    }

    [Fact]
    public void Enabled_true_when_level_positive_and_routing_present()
    {
        var opts = new ClickOptions { Level = 0.5, Bpm = 120 };
        var click = new NaiveClickSource(opts, hasRouting: true);
        click.Enabled.Should().BeTrue();
    }
}
