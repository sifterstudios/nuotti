using FluentAssertions;
using Nuotti.AudioEngine.Playback;
using Nuotti.AudioEngine.Tests.Fakes;
using System;
using System.Diagnostics;
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

    [Fact]
    public async Task StopAsync_Then_PlayAsync_Replaces_Running_Playback()
    {
        // Arrange: start a long-running process
        var fakeRunner = new FakeProcessRunner();
        (string file, string args)? Resolver(string url, PreferredPlayer p) => ("mockplayer", "--dummy");

        using var player = new SystemPlayer(PreferredPlayer.Auto, Resolver, fakeRunner);
        var startedCount = 0;
        var stoppedCount = 0;
        var startedEvent = new TaskCompletionSource();
        var stoppedEvent = new TaskCompletionSource();
        player.Started += (_, __) => { startedCount++; startedEvent.TrySetResult(); };
        player.Stopped += (_, __) => { stoppedCount++; stoppedEvent.TrySetResult(); };

        // Act: start first playback
        await player.PlayAsync("url1://test");
        await Task.WhenAny(startedEvent.Task, Task.Delay(100));
        startedCount.Should().Be(1);
        player.IsPlaying.Should().BeTrue();

        // Stop first playback
        await player.StopAsync();
        await Task.WhenAny(stoppedEvent.Task, Task.Delay(100));
        stoppedCount.Should().Be(1);
        player.IsPlaying.Should().BeFalse();

        // Start second playback after stop
        startedEvent = new TaskCompletionSource();
        await player.PlayAsync("url2://test");

        // Assert: second playback should start
        await Task.WhenAny(startedEvent.Task, Task.Delay(100));
        startedCount.Should().Be(2, "second playback should start after stop");
        player.IsPlaying.Should().BeTrue();
    }

    [Fact]
    public async Task PlayAsync_With_Null_Resolver_Returns_Error()
    {
        // Arrange: resolver that returns null (no supported player)
        var fakeRunner = new FakeProcessRunner();
        (string file, string args)? Resolver(string url, PreferredPlayer p) => null;

        using var player = new SystemPlayer(PreferredPlayer.Auto, Resolver, fakeRunner);
        Exception? error = null;
        var errorEvent = new TaskCompletionSource();
        player.Error += (_, e) => { error = e; errorEvent.TrySetResult(); };

        // Act: try to play
        await player.PlayAsync("http://example.com/audio.mp3");
        await Task.WhenAny(errorEvent.Task, Task.Delay(100));

        // Assert: error should be raised
        error.Should().NotBeNull();
        error!.Message.Should().Contain("No supported player found");
        player.IsPlaying.Should().BeFalse();
        fakeRunner.Started.Should().BeEmpty("no process should be started");
    }

    [Fact]
    public async Task PlayAsync_When_Process_Fails_To_Start_Raises_Error()
    {
        // Arrange: custom runner that returns a handle with Start() returning false
        var customRunner = new FailingStartProcessRunner();
        (string file, string args)? Resolver(string url, PreferredPlayer p) => ("mockplayer", "--dummy");

        using var player = new SystemPlayer(PreferredPlayer.Auto, Resolver, customRunner);
        Exception? error = null;
        var errorEvent = new TaskCompletionSource();
        player.Error += (_, e) => { error = e; errorEvent.TrySetResult(); };

        // Act: try to play
        await player.PlayAsync("http://example.com/audio.mp3");
        await Task.WhenAny(errorEvent.Task, Task.Delay(200));

        // Assert: error should be raised
        error.Should().NotBeNull("error should be raised when process fails to start");
        error!.Message.Should().Contain("Failed to start player process");
        player.IsPlaying.Should().BeFalse();
    }

    [Fact]
    public async Task PlayAsync_Ignores_Duplicate_Calls_While_Playing()
    {
        // Arrange: start a playback
        var fakeRunner = new FakeProcessRunner();
        (string file, string args)? Resolver(string url, PreferredPlayer p) => ("mockplayer", "--dummy");

        using var player = new SystemPlayer(PreferredPlayer.Auto, Resolver, fakeRunner);
        var startedCount = 0;
        var startedEvent = new TaskCompletionSource();
        player.Started += (_, __) => { startedCount++; startedEvent.TrySetResult(); };

        // Act: start first playback
        await player.PlayAsync("url1://test");
        await Task.WhenAny(startedEvent.Task, Task.Delay(100));
        startedCount.Should().Be(1);
        player.IsPlaying.Should().BeTrue();

        // Try to start again while playing (should be ignored)
        startedEvent = new TaskCompletionSource();
        await player.PlayAsync("url2://test");
        await Task.Delay(50); // Small delay to ensure no second start

        // Assert: only one process should have started
        startedCount.Should().Be(1, "duplicate PlayAsync calls should be ignored while playing");
        fakeRunner.Started.Should().HaveCount(1);
    }

    [Fact]
    public void PlayAsync_Throws_When_Disposed()
    {
        // Arrange: dispose the player
        var fakeRunner = new FakeProcessRunner();
        (string file, string args)? Resolver(string url, PreferredPlayer p) => ("mockplayer", "--dummy");

        var player = new SystemPlayer(PreferredPlayer.Auto, Resolver, fakeRunner);
        player.Dispose();

        // Act & Assert: should throw ObjectDisposedException
        player.Invoking(p => p.PlayAsync("http://example.com/audio.mp3"))
            .Should().ThrowAsync<ObjectDisposedException>();
    }

    [Fact]
    public void PlayAsync_Throws_When_Url_Is_Null_Or_Empty()
    {
        // Arrange
        var fakeRunner = new FakeProcessRunner();
        (string file, string args)? Resolver(string url, PreferredPlayer p) => ("mockplayer", "--dummy");

        using var player = new SystemPlayer(PreferredPlayer.Auto, Resolver, fakeRunner);

        // Act & Assert: should throw ArgumentException
        player.Invoking(p => p.PlayAsync(null!))
            .Should().ThrowAsync<ArgumentException>()
            .WithMessage("*URL is required*");

        player.Invoking(p => p.PlayAsync(""))
            .Should().ThrowAsync<ArgumentException>()
            .WithMessage("*URL is required*");

        player.Invoking(p => p.PlayAsync("   "))
            .Should().ThrowAsync<ArgumentException>()
            .WithMessage("*URL is required*");
    }

    [Fact]
    public async Task StopAsync_When_Not_Playing_Does_Not_Raise_Stopped_Event()
    {
        // Arrange: player that's not playing
        var fakeRunner = new FakeProcessRunner();
        (string file, string args)? Resolver(string url, PreferredPlayer p) => ("mockplayer", "--dummy");

        using var player = new SystemPlayer(PreferredPlayer.Auto, Resolver, fakeRunner);
        var stoppedRaised = false;
        player.Stopped += (_, __) => { stoppedRaised = true; };

        // Act: stop when not playing
        await player.StopAsync();

        // Assert: Stopped event should not be raised
        await Task.Delay(50);
        stoppedRaised.Should().BeFalse("Stopped event should not be raised when not playing");
    }
}

/// <summary>
/// Fake process runner that simulates a process handle with Start() returning false.
/// </summary>
file sealed class FailingStartProcessRunner : IProcessRunner
{
    public IProcessHandle Create(ProcessStartInfo startInfo, bool enableRaisingEvents = true)
    {
        return new FailingStartProcessHandle(startInfo);
    }

    public bool CanStart(string fileName) => true;
}

file sealed class FailingStartProcessHandle : IProcessHandle
{
    private readonly ProcessStartInfo _startInfo;

    public FailingStartProcessHandle(ProcessStartInfo startInfo)
    {
        _startInfo = startInfo;
    }

    public ProcessStartInfo StartInfo => _startInfo;
    public bool HasExited => true;
    public event EventHandler? Exited;

    public bool Start() => false; // Simulate failure to start

    public void Kill(bool entireProcessTree) { }
    public void Dispose() { }
}