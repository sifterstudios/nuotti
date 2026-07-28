using Nuotti.Contracts.V1.Enum;
using Nuotti.Contracts.V1.Model;
using Nuotti.SimKit.Actors;
using Nuotti.SimKit.Hub;
using Nuotti.SimKit.Time;
using System.Diagnostics;
using Xunit;
namespace Nuotti.SimKit.Tests;

public class ChaosDisconnectTests
{
    [Fact]
    public async Task After_churn_run_final_GameState_sequence_still_completes()
    {
        // Base client that can fire snapshots immediately.
        var baseFactory = new TriggeringHubClientFactory();

        // Wrap with chaos injector: moderate probability and short downtime.
        var chaosResolver = new DictionaryChaosPolicyResolver(new Dictionary<string, ChaosPolicy>
        {
            ["Projector"] = new ChaosPolicy(Probability: 0.2, MinDowntime: TimeSpan.FromMilliseconds(5), MaxDowntime: TimeSpan.FromMilliseconds(20), ApplyToReceives: true)
        });
        var chaoticFactory = new ChaosInjectingHubClientFactory(
            baseFactory,
            chaosResolver,
            new ImmediateTimeProvider(),
            () => DeterministicRandom.ForLane(seed: 1, laneIndex: 0));

        var actor = new ProjectorActor(chaoticFactory, new Uri("http://localhost:5000"), "SESS");
        await actor.StartAsync();

        var client = baseFactory.Client!;

        // Send a churn of states
        int total = 50;
        for (int i = 0; i < total; i++)
        {
            var phase = (Phase)(i % 5); // cycle through phases
            client.Fire(new GameStateSnapshot(
                sessionCode: "SESS",
                phase: phase,
                songIndex: 0,
                currentSong: null,
                choices: null,
                hintIndex: 0,
                tallies: null,
                scores: null,
                songStartedAtUtc: null
            ));
            await Task.Delay(2);
        }

        // Wait until the actor has received all items or timeout
        var sw = Stopwatch.StartNew();
        while (actor.ReceivedPhases.Count < total && sw.Elapsed < TimeSpan.FromSeconds(10))
        {
            await Task.Delay(10);
        }

        Assert.Equal(total, actor.ReceivedPhases.Count);

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
    Func<GameStateSnapshot, Task>? _handler;

    public Task StartAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
    public Task StopAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
    public Task JoinAsync(string session, string role, string? name = null, CancellationToken cancellationToken = default) => Task.CompletedTask;
    public Task SubmitAnswerAsync(string session, int choiceIndex, CancellationToken cancellationToken = default) => Task.CompletedTask;

    public IDisposable OnGameStateChanged(Func<GameStateSnapshot, Task> handler)
    {
        _handler = handler;
        return new Unsubscriber(() => _handler = null);
    }

    // Fire-and-forget, same as the Action<T> version this replaces: the caller does not wait
    // for the handler to finish, so we deliberately do not await here either.
    public void Fire(GameStateSnapshot snapshot) => _handler?.Invoke(snapshot);

    sealed class Unsubscriber(Action dispose) : IDisposable
    {
        public void Dispose() => dispose();
    }
}
