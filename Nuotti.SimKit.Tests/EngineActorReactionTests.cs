using FluentAssertions;
using Nuotti.Contracts.V1.Enum;
using Nuotti.Contracts.V1.Message;
using Nuotti.Contracts.V1.Model;
using Nuotti.SimKit.Actors;
using Nuotti.SimKit.Hub;
using Nuotti.SimKit.Time;
using Xunit;

namespace Nuotti.SimKit.Tests;

public class EngineActorReactionTests
{
    static PlayTrack APlayTrack() => new("file:///song.mp3")
    {
        SessionCode = "dev", IssuedByRole = Role.Performer, IssuedById = "perf-1"
    };

    static StopTrack AStopTrack() => new()
    {
        SessionCode = "dev", IssuedByRole = Role.Performer, IssuedById = "perf-1"
    };

    [Fact]
    public async Task Reports_playing_when_a_play_track_arrives()
    {
        var factory = new RelayHubClientFactory();
        var actor = new EngineActor(factory, new Uri("http://in-proc"), "dev",
            LaneRandom.ForLane(seed: 3, laneIndex: 0), failureRate: 0);

        await actor.StartAsync();
        await factory.Client!.PushAsync(APlayTrack());

        actor.Emitted.Should().ContainSingle()
            .Which.Status.Should().Be(EngineStatus.Playing);
    }

    [Fact]
    public async Task Reports_ready_when_a_stop_arrives()
    {
        var factory = new RelayHubClientFactory();
        var actor = new EngineActor(factory, new Uri("http://in-proc"), "dev",
            LaneRandom.ForLane(seed: 3, laneIndex: 0), failureRate: 0);

        await actor.StartAsync();
        await factory.Client!.PushAsync(AStopTrack());

        actor.Emitted.Should().ContainSingle()
            .Which.Status.Should().Be(EngineStatus.Ready);
    }

    [Fact]
    public async Task Reports_error_when_the_failure_rate_is_certain()
    {
        var factory = new RelayHubClientFactory();
        var actor = new EngineActor(factory, new Uri("http://in-proc"), "dev",
            LaneRandom.ForLane(seed: 3, laneIndex: 0), failureRate: 1.0);

        await actor.StartAsync();
        await factory.Client!.PushAsync(APlayTrack());

        actor.Emitted.Should().ContainSingle()
            .Which.Status.Should().Be(EngineStatus.Error);
    }

    [Fact]
    public async Task Stops_reacting_once_the_actor_stops()
    {
        var factory = new RelayHubClientFactory();
        var actor = new EngineActor(factory, new Uri("http://in-proc"), "dev",
            LaneRandom.ForLane(seed: 3, laneIndex: 0), failureRate: 0);

        await actor.StartAsync();
        await actor.StopAsync();
        await factory.Client!.PushAsync(APlayTrack());

        actor.Emitted.Should().BeEmpty();
    }
}

file sealed class RelayHubClientFactory : IHubClientFactory
{
    public RelayHubClient? Client { get; private set; }
    public IHubClient Create(Uri baseAddress) => Client = new RelayHubClient();
}

file sealed class RelayHubClient : IHubClient
{
    readonly Dictionary<Type, Func<object, Task>> _handlers = new();

    public Task StartAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
    public Task StopAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
    public Task JoinAsync(string session, string role, string? name = null, CancellationToken cancellationToken = default) => Task.CompletedTask;
    public Task SubmitAnswerAsync(string session, int choiceIndex, CancellationToken cancellationToken = default) => Task.CompletedTask;

    public IDisposable On<T>(Func<T, Task> handler)
    {
        _handlers[typeof(T)] = payload => handler((T)payload);
        return new Sub(this, typeof(T));
    }

    public Task PushAsync<T>(T payload) where T : notnull =>
        _handlers.TryGetValue(typeof(T), out var h) ? h(payload) : Task.CompletedTask;

    private sealed class Sub(RelayHubClient owner, Type key) : IDisposable
    {
        public void Dispose() => owner._handlers.Remove(key);
    }
}
