using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Nuotti.AudioEngine.Playback;
using Nuotti.Contracts.V1.Enum;
using Nuotti.Contracts.V1.Message;
using Xunit;

namespace Nuotti.AudioEngine.Tests;

public sealed class VenueEngineHostTests
{
    [Fact]
    public void BuildHubUrl_targets_the_engine_hub_not_a_projector_role()
    {
        var url = VenueEngineHost.BuildHubUrl("https://api.nuotti.app/");
        Assert.Equal("https://api.nuotti.app/hub?deviceRole=engine", url);
        Assert.Contains("deviceRole=engine", url, StringComparison.Ordinal);
    }

    [Fact]
    public async Task StartAsync_connects_the_transport_and_registers_play_handlers()
    {
        var transport = new FakeTransport();
        await using var host = new VenueEngineHost(transport, new NoopPlayer());

        await host.StartAsync();

        Assert.True(host.IsStarted);
        Assert.True(transport.Connected);
        Assert.NotNull(transport.PlayTrackHandler);
        Assert.NotNull(transport.TrackPlayHandler);
        Assert.NotNull(transport.TrackStoppedHandler);
        Assert.Contains(transport.Statuses, s => s.Status == EngineStatus.Ready);
    }

    [Fact]
    public async Task PlayTrack_on_the_transport_reaches_the_player()
    {
        var transport = new FakeTransport();
        var player = new RecordingPlayer();
        await using var host = new VenueEngineHost(transport, player);
        await host.StartAsync();

        await transport.PlayTrackHandler!(new PlayTrack("https://cdn.example/a.mp3")
        {
            SessionCode = "SHOW1",
            IssuedByRole = Role.Performer,
            IssuedById = "p1"
        });

        Assert.Equal("https://cdn.example/a.mp3", player.LastUrl);
    }

    [Fact]
    public async Task StopAsync_is_safe_before_start()
    {
        var transport = new FakeTransport();
        await using var host = new VenueEngineHost(transport, new NoopPlayer());
        await host.StopAsync();
        Assert.False(host.IsStarted);
    }

    sealed class FakeTransport : IVenueEngineTransport
    {
        public bool Connected { get; private set; }
        public Func<PlayTrack, Task>? PlayTrackHandler { get; private set; }
        public Func<string, Task>? TrackPlayHandler { get; private set; }
        public Func<Task>? TrackStoppedHandler { get; private set; }
        public List<EngineStatusChanged> Statuses { get; } = [];

        public Task ConnectAsync(CancellationToken cancellationToken = default)
        {
            Connected = true;
            return Task.CompletedTask;
        }

        public void OnPlayTrack(Func<PlayTrack, Task> handler) => PlayTrackHandler = handler;
        public void OnTrackPlayRequested(Func<string, Task> handler) => TrackPlayHandler = handler;
        public void OnTrackStopped(Func<Task> handler) => TrackStoppedHandler = handler;

        public Task ReportStatusAsync(EngineStatusChanged status, CancellationToken cancellationToken = default)
        {
            Statuses.Add(status);
            return Task.CompletedTask;
        }

        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }

    sealed class RecordingPlayer : IAudioPlayer
    {
        public string? LastUrl { get; private set; }
        public bool IsPlaying { get; private set; }
        public event EventHandler? Started;
        public event EventHandler<bool>? Stopped;
        public event EventHandler<Exception>? Error;

        public Task PlayAsync(string url, CancellationToken cancellationToken = default)
        {
            LastUrl = url;
            IsPlaying = true;
            Started?.Invoke(this, EventArgs.Empty);
            return Task.CompletedTask;
        }

        public Task StopAsync(CancellationToken cancellationToken = default)
        {
            if (!IsPlaying) return Task.CompletedTask;
            IsPlaying = false;
            Stopped?.Invoke(this, false);
            return Task.CompletedTask;
        }
    }
}
