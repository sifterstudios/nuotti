using Nuotti.Contracts.V1.Enum;
using Nuotti.Contracts.V1.Model;
using Nuotti.SimKit.Actors;
using Nuotti.SimKit.Hub;
using Xunit;
namespace Nuotti.SimKit.Tests;

public class ProjectorActorStateSubscriptionTests
{
    [Fact]
    public async Task Receives_phase_sequence_in_order_for_happy_path()
    {
        var factory = new TriggeringHubClientFactory();
        var actor = new ProjectorActor(factory, new Uri("http://localhost:5000"), "SESS");
        await actor.StartAsync();

        var client = factory.Client!;
        // Simulate server broadcasting snapshots
        client.Fire(new GameStateSnapshot(
            sessionCode: "SESS",
            phase: Phase.Lobby,
            songIndex: 0,
            currentSong: null,
            choices: null,
            hintIndex: 0,
            tallies: null,
            scores: null,
            songStartedAtUtc: null
        ));
        client.Fire(new GameStateSnapshot(
            sessionCode: "SESS",
            phase: Phase.Start,
            songIndex: 0,
            currentSong: null,
            choices: null,
            hintIndex: 0,
            tallies: null,
            scores: null,
            songStartedAtUtc: null
        ));
        client.Fire(new GameStateSnapshot(
            sessionCode: "SESS",
            phase: Phase.Play,
            songIndex: 0,
            currentSong: null,
            choices: null,
            hintIndex: 0,
            tallies: null,
            scores: null,
            songStartedAtUtc: null
        ));

        Assert.Equal([Phase.Lobby, Phase.Start, Phase.Play], actor.ReceivedPhases);

        await actor.StopAsync();
    }
}

file sealed class TriggeringHubClientFactory : IHubClientFactory
{
    public TriggeringHubClient? Client { get; private set; }
    public IHubClient Create(Uri baseAddress)
    {
        Client = new TriggeringHubClient();
        return Client;
    }
}

file sealed class TriggeringHubClient : IHubClient
{
    Action<GameStateSnapshot>? _handler;

    public Task StartAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
    public Task StopAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
    public Task JoinAsync(string session, string role, string? name = null, CancellationToken cancellationToken = default) => Task.CompletedTask;
    public Task SubmitAnswerAsync(string session, int choiceIndex, CancellationToken cancellationToken = default) => Task.CompletedTask;

    public IDisposable OnGameStateChanged(Action<GameStateSnapshot> handler)
    {
        _handler = handler;
        return new Unsubscriber(() => _handler = null);
    }

    public void Fire(GameStateSnapshot snapshot) => _handler?.Invoke(snapshot);

    sealed class Unsubscriber(Action dispose) : IDisposable
    {
        public void Dispose() => dispose();
    }
}
