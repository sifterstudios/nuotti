using FluentAssertions;
using Nuotti.AudioEngine.Playback;
using Nuotti.AudioEngine.Tests.Fakes;
using System.Threading.Tasks;
using Xunit;
namespace Nuotti.AudioEngine.Tests;

public class SystemPlayerTests
{
    [Fact]
    public async Task StopAsync_KillsLongRunningProcess()
    {
        // Arrange: use fake process runner that never exits until killed
        var fakeRunner = new FakeProcessRunner();
        (string file, string args)? Resolver(string url, PreferredPlayer p) => ("mockplayer", "--dummy");

        using var player = new SystemPlayer(PreferredPlayer.Auto, Resolver, fakeRunner);
        bool started = false;
        bool? cancelled = null;
        var startedEvent = new TaskCompletionSource();
        var stoppedEvent = new TaskCompletionSource();
        player.Started += (_, __) => { started = true; startedEvent.TrySetResult(); };
        player.Stopped += (_, c) => { cancelled = c; stoppedEvent.TrySetResult(); };

        // Act: start and ensure it's playing, then stop
        await player.PlayAsync("ignored://url");
        await Task.WhenAny(startedEvent.Task, Task.Delay(100));
        started.Should().BeTrue("process should start");
        player.IsPlaying.Should().BeTrue();

        await player.StopAsync();
        await Task.WhenAny(stoppedEvent.Task, Task.Delay(100));

        // Assert
        cancelled.Should().BeTrue("stop should be treated as cancelled");
        player.IsPlaying.Should().BeFalse();
    }
    [Fact]
    public async Task Dispose_KillsLongRunningProcessWithoutExplicitStop()
    {
        // Arrange: start a long-running process using fake runner
        var fakeRunner = new FakeProcessRunner();
        (string file, string args)? Resolver(string url, PreferredPlayer p) => ("mockplayer", "--dummy");

        var player = new SystemPlayer(PreferredPlayer.Auto, Resolver, fakeRunner);
        bool started = false;
        bool? cancelled = null;
        var startedEvent = new TaskCompletionSource();
        var stoppedEvent = new TaskCompletionSource();
        player.Started += (_, __) => { started = true; startedEvent.TrySetResult(); };
        player.Stopped += (_, c) => { cancelled = c; stoppedEvent.TrySetResult(); };

        await player.PlayAsync("ignored://url");
        await Task.WhenAny(startedEvent.Task, Task.Delay(100));
        started.Should().BeTrue();

        // Act: Dispose without calling StopAsync
        player.Dispose();

        // Assert: process should be killed, raising Stopped with cancelled=true
        await Task.WhenAny(stoppedEvent.Task, Task.Delay(100));
        cancelled.Should().BeTrue();
    }
}