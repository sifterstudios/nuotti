using FluentAssertions;
using Nuotti.Contracts.V1.Enum;
using Nuotti.Contracts.V1.Model;
using Nuotti.SimKit.Actors;
using Nuotti.SimKit.Hub;
using Nuotti.SimKit.Time;
using Xunit;

namespace Nuotti.SimKit.Tests;

public class AudienceActorSubscriptionTests
{
    static AudienceActor AnAudience(IHubClientFactory factory) =>
        new(factory, new Uri("http://in-proc"), "dev", "aud-1",
            random: LaneRandom.ForLane(seed: 1, laneIndex: 0),
            options: new AudienceOptions { MinDelay = TimeSpan.Zero, MaxDelay = TimeSpan.Zero },
            timeProvider: new ImmediateTimeProvider());

    static GameStateSnapshot AGuessingSnapshot() => new(
        sessionCode: "dev",
        phase: Phase.Guessing,
        songIndex: 0,
        currentSong: new SongRef(new SongId("song-1"), "Title", "Artist"),
        choices: new[] { "a", "b", "c", "d" },
        hintIndex: 0,
        tallies: new[] { 0, 0, 0, 0 },
        scores: null,
        songStartedAtUtc: DateTime.UtcNow
    );

    [Fact]
    public async Task Answers_a_guessing_snapshot_without_being_called_directly()
    {
        // The actor must wire itself up on start. Before this, OnStateAsync existed but
        // nothing ever invoked it, so a simulated audience never answered.
        var factory = new PushingHubClientFactory();
        var actor = AnAudience(factory);

        await actor.StartAsync();
        await factory.Client!.PushAsync(AGuessingSnapshot());

        factory.Client.Answers.Should().HaveCount(1);
    }

    [Fact]
    public async Task Stops_answering_once_the_actor_stops()
    {
        var factory = new PushingHubClientFactory();
        var actor = AnAudience(factory);

        await actor.StartAsync();
        await actor.StopAsync();
        await factory.Client!.PushAsync(AGuessingSnapshot());

        factory.Client.Answers.Should().BeEmpty();
    }

    [Fact]
    public async Task Keeps_answering_after_the_token_passed_to_start_is_cancelled()
    {
        var factory = new PushingHubClientFactory();
        var actor = AnAudience(factory);

        using var startScope = new CancellationTokenSource();
        await actor.StartAsync(startScope.Token);
        startScope.Cancel();

        // The start call's token governs starting, not the subscription's lifetime. An
        // audience that goes silent when a connect timeout elapses is the bug this guards.
        await factory.Client!.PushAsync(AGuessingSnapshot());

        factory.Client.Answers.Should().HaveCount(1);
    }
}

file sealed class PushingHubClientFactory : IHubClientFactory
{
    public PushingHubClient? Client { get; private set; }
    public IHubClient Create(Uri baseAddress) => Client = new PushingHubClient();
}

file sealed class PushingHubClient : IHubClient
{
    Func<GameStateSnapshot, Task>? _onSnapshot;

    public List<(string Session, int ChoiceIndex)> Answers { get; } = new();

    public Task StartAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
    public Task StopAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
    public Task JoinAsync(string session, string role, string? name = null, CancellationToken cancellationToken = default) => Task.CompletedTask;

    public Task SubmitAnswerAsync(string session, int choiceIndex, CancellationToken cancellationToken = default)
    {
        Answers.Add((session, choiceIndex));
        return Task.CompletedTask;
    }

    public IDisposable On<T>(Func<T, Task> handler)
    {
        if (typeof(T) == typeof(GameStateSnapshot))
            _onSnapshot = s => handler((T)(object)s);
        return new Sub(this);
    }

    // Awaited, not discarded: a discarded Task would reintroduce the async-void hazard
    // stage 1 removed, and would make these assertions race.
    public Task PushAsync(GameStateSnapshot snapshot) => _onSnapshot?.Invoke(snapshot) ?? Task.CompletedTask;

    sealed class Sub(PushingHubClient owner) : IDisposable
    {
        public void Dispose() => owner._onSnapshot = null;
    }
}
