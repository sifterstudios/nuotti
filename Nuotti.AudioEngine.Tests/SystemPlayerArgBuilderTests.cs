using FluentAssertions;
using Nuotti.AudioEngine.Playback;
using Nuotti.AudioEngine.Tests.Fakes;
using System;
using System.Threading.Tasks;
using Xunit;
namespace Nuotti.AudioEngine.Tests;

public class SystemPlayerArgBuilderTests
{
    private static string Url => "http://example.com/audio.mp3";

    [Fact]
    public async Task MacOS_Uses_Afplay_With_Quoted_Url()
    {
        if (!OperatingSystem.IsMacOS()) return; // only validate on macOS agents
        var runner = new FakeProcessRunner(new[] { "afplay" });
        using var player = new SystemPlayer(PreferredPlayer.Auto, resolver: null, runner: runner);

        await player.PlayAsync(Url);

        runner.Started.Should().HaveCount(1);
        var psi = runner.Started[0].StartInfo;
        psi.FileName.Should().Be("afplay");
        psi.Arguments.Should().Be('"' + Url + '"');
    }

    [Fact]
    public async Task Windows_Prefers_Ffplay_With_NoDisp_AutoExit_And_Quoted_Url()
    {
        if (!OperatingSystem.IsWindows()) return; // only on Windows
        var runner = new FakeProcessRunner(new[] { "ffplay" });
        using var player = new SystemPlayer(PreferredPlayer.Auto, resolver: null, runner: runner);

        await player.PlayAsync(Url);

        runner.Started.Should().HaveCount(1);
        var psi = runner.Started[0].StartInfo;
        psi.FileName.Should().Be("ffplay");
        psi.Arguments.Should().Be($"-nodisp -autoexit \"{Url}\"");
    }

    [Fact]
    public async Task Linux_Prefers_Ffplay_With_NoDisp_AutoExit_And_Quoted_Url()
    {
        if (OperatingSystem.IsWindows() || OperatingSystem.IsMacOS()) return; // only on Linux/other
        var runner = new FakeProcessRunner(new[] { "ffplay" });
        using var player = new SystemPlayer(PreferredPlayer.Auto, resolver: null, runner: runner);

        await player.PlayAsync(Url);

        runner.Started.Should().HaveCount(1);
        var psi = runner.Started[0].StartInfo;
        psi.FileName.Should().Be("ffplay");
        psi.Arguments.Should().Be($"-nodisp -autoexit \"{Url}\"");
    }

    [Fact]
    public async Task Preferred_Vlc_Is_Used_When_Available()
    {
        var runner = new FakeProcessRunner(new[] { "vlc" });
        using var player = new SystemPlayer(PreferredPlayer.Vlc, resolver: null, runner: runner);

        await player.PlayAsync(Url);

        runner.Started.Should().HaveCount(1);
        var psi = runner.Started[0].StartInfo;
        psi.FileName.Should().Be("vlc");
        psi.Arguments.Should().Be($"--play-and-exit \"{Url}\"");
    }
}
