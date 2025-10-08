using Nuotti.Contracts.V1.Enum;
using Nuotti.Contracts.V1.Model;
using Nuotti.SimKit.Actors;
using Nuotti.SimKit.Hub;
using Xunit;
namespace Nuotti.SimKit.Tests;

public class EngineActorLifecycleTests
{
    [Fact]
    public async Task Emits_Playing_then_Ready_on_happy_path()
    {
        var factory = new FakeHubClientFactory();
        var actor = new EngineActor(factory, new Uri("http://localhost:5000"), "GAME01");
        await actor.StartAsync();

        actor.OnTrackPlayRequested();
        actor.OnTrackStopped();

        Assert.Collection(actor.Emitted,
            e => Assert.Equal(EngineStatus.Playing, e.Status),
            e => Assert.Equal(EngineStatus.Ready, e.Status)
        );
    }

    [Fact]
    public async Task Emits_Error_on_play_when_failureRate_is_one()
    {
        var factory = new FakeHubClientFactory();
        // Inject deterministic random, but with failureRate = 1 it should always fail regardless of RNG
        var actor = new EngineActor(factory, new Uri("http://localhost:5000"), "GAME01", failureRate: 1.0, random: new Random(123));
        await actor.StartAsync();

        actor.OnTrackPlayRequested();

        Assert.Single(actor.Emitted);
        Assert.Equal(EngineStatus.Error, actor.Emitted[0].Status);
    }
}

file sealed class FakeHubClientFactory : IHubClientFactory
{
    public FakeHubClient? Client { get; private set; }
    public IHubClient Create(Uri baseAddress)
    {
        Client = new FakeHubClient(baseAddress);
        return Client;
    }
}

file sealed class FakeHubClient : IHubClient
{
    public Uri BaseAddress { get; }
    public FakeHubClient(Uri baseAddress) => BaseAddress = baseAddress;
    public Task StartAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
    public Task StopAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
    public Task JoinAsync(string session, string role, string? name = null, CancellationToken cancellationToken = default) => Task.CompletedTask;
    public Task SubmitAnswerAsync(string session, int choiceIndex, CancellationToken cancellationToken = default) => Task.CompletedTask;
    public IDisposable OnGameStateChanged(Action<GameStateSnapshot> handler) => new NoopDisposable();
    sealed class NoopDisposable : IDisposable { public void Dispose() { } }
}
