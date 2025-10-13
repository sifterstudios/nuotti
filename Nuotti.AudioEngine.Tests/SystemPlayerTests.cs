using FluentAssertions;
using Nuotti.AudioEngine.Playback;
using System;
using System.Threading.Tasks;
using Xunit;
namespace Nuotti.AudioEngine.Tests;

public class SystemPlayerTests
{
    [Fact]
    public async Task StopAsync_KillsLongRunningProcess()
    {
        // Arrange: inject a resolver that starts a long-running sleep depending on OS
        (string file, string args)? Resolver(string url, PreferredPlayer p)
        {
            if (OperatingSystem.IsWindows())
            {
                return ("powershell", "-NoProfile -Command Start-Sleep -Seconds 60");
            }
            else
            {
                return ("bash", "-lc 'sleep 60'");
            }
        }

        using var player = new SystemPlayer(PreferredPlayer.Auto, Resolver);
        bool started = false;
        bool? cancelled = null;
        var startedEvent = new TaskCompletionSource();
        var stoppedEvent = new TaskCompletionSource();
        player.Started += (_, __) => { started = true; startedEvent.TrySetResult(); };
        player.Stopped += (_, c) => { cancelled = c; stoppedEvent.TrySetResult(); };

        // Act: start and ensure it's playing, then stop
        await player.PlayAsync("ignored://url");
        await Task.WhenAny(startedEvent.Task, Task.Delay(3000));
        started.Should().BeTrue("process should start");
        player.IsPlaying.Should().BeTrue();

        await player.StopAsync();
        await Task.WhenAny(stoppedEvent.Task, Task.Delay(5000));

        // Assert
        cancelled.Should().BeTrue("stop should be treated as cancelled");
        player.IsPlaying.Should().BeFalse();
    }
    [Fact]
    public async Task Dispose_KillsLongRunningProcessWithoutExplicitStop()
    {
        // Arrange: start a long-running process
        (string file, string args)? Resolver(string url, PreferredPlayer p)
        {
            if (OperatingSystem.IsWindows())
            {
                return ("powershell", "-NoProfile -Command Start-Sleep -Seconds 60");
            }
            else
            {
                return ("bash", "-lc 'sleep 60'");
            }
        }

        var player = new SystemPlayer(PreferredPlayer.Auto, Resolver);
        bool started = false;
        bool? cancelled = null;
        var startedEvent = new TaskCompletionSource();
        var stoppedEvent = new TaskCompletionSource();
        player.Started += (_, __) => { started = true; startedEvent.TrySetResult(); };
        player.Stopped += (_, c) => { cancelled = c; stoppedEvent.TrySetResult(); };

        await player.PlayAsync("ignored://url");
        await Task.WhenAny(startedEvent.Task, Task.Delay(3000));
        started.Should().BeTrue();

        // Act: Dispose without calling StopAsync
        player.Dispose();

        // Assert: process should be killed, raising Stopped with cancelled=true
        await Task.WhenAny(stoppedEvent.Task, Task.Delay(5000));
        cancelled.Should().BeTrue();
    }
}