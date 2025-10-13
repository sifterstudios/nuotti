using FluentAssertions;
using Nuotti.AudioEngine.Playback;
using Nuotti.Contracts.V1.Enum;
using Nuotti.Contracts.V1.Message;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Xunit;
namespace Nuotti.AudioEngine.Tests;

public class EngineCoordinatorTests
{
    private sealed class ListSink : IEngineStatusSink
    {
        public List<EngineStatusChanged> Events { get; } = new();
        public Task PublishAsync(EngineStatusChanged evt, CancellationToken cancellationToken = default)
        {
            Events.Add(evt);
            return Task.CompletedTask;
        }
    }

    private sealed class ErrorPlayer : IAudioPlayer
    {
        public event EventHandler? Started;
        public event EventHandler<bool>? Stopped;
        public event EventHandler<Exception>? Error;
        public bool IsPlaying { get; private set; }
        public Task PlayAsync(string url, CancellationToken cancellationToken = default)
        {
            Error?.Invoke(this, new InvalidOperationException("boom"));
            return Task.CompletedTask;
        }
        public Task StopAsync(CancellationToken cancellationToken = default)
        {
            if (IsPlaying)
            {
                IsPlaying = false;
                Stopped?.Invoke(this, true);
            }
            return Task.CompletedTask;
        }
    }

    [Fact]
    public async Task Play_then_Stop_emits_Playing_then_Ready()
    {
        var player = new NoopPlayer();
        var sink = new ListSink();
        var engine = new EngineCoordinator(player, sink);

        await engine.OnTrackPlayRequested("http://example.com/a.mp3");
        // NoopPlayer raises Started synchronously
        sink.Events.Should().ContainSingle(e => e.Status == EngineStatus.Playing);

        await engine.OnTrackStopped();
        sink.Events.Should().Contain(e => e.Status == EngineStatus.Ready);
    }

    [Fact]
    public async Task Play_while_playing_stops_then_plays_again()
    {
        var player = new NoopPlayer();
        var sink = new ListSink();
        var engine = new EngineCoordinator(player, sink);

        await engine.OnTrackPlayRequested("http://example.com/a.mp3");
        await engine.OnTrackPlayRequested("http://example.com/b.mp3");

        // Expected sequence: Playing (first), Ready (stop), Playing (second)
        sink.Events.Should().ContainInOrder(
            new EngineStatusChanged(EngineStatus.Playing, 0),
            new EngineStatusChanged(EngineStatus.Ready, 0),
            new EngineStatusChanged(EngineStatus.Playing, 0)
        );
    }

    [Fact]
    public async Task Error_on_play_emits_Error()
    {
        var player = new ErrorPlayer();
        var sink = new ListSink();
        var engine = new EngineCoordinator(player, sink);

        await engine.OnTrackPlayRequested("http://example.com/err.mp3");

        sink.Events.Should().ContainSingle(e => e.Status == EngineStatus.Error);
    }
}
