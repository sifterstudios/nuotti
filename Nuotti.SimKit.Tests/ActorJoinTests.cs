using Nuotti.Contracts.V1.Model;
using Nuotti.SimKit.Actors;
using Nuotti.SimKit.Hub;
using Xunit;
namespace Nuotti.SimKit.Tests;

public class ActorJoinTests
{
    [Fact]
    public async Task Performer_joins_session_and_role()
    {
        var factory = new FakeHubClientFactory();
        var actor = new PerformerActor(factory, new Uri("http://localhost:5000"), "ABCD");

        await actor.StartAsync();

        var call = factory.Client!.Calls.Single();
        Assert.Equal("ABCD", call.Session);
        Assert.Equal("performer", call.Role);
        Assert.Null(call.Name);
    }

    [Fact]
    public async Task Projector_joins_session_and_role()
    {
        var factory = new FakeHubClientFactory();
        var actor = new ProjectorActor(factory, new Uri("http://localhost:5000"), "SESS");

        await actor.StartAsync();

        var call = factory.Client!.Calls.Single();
        Assert.Equal("SESS", call.Session);
        Assert.Equal("projector", call.Role);
        Assert.Null(call.Name);
    }

    [Fact]
    public async Task Engine_joins_session_and_role()
    {
        var factory = new FakeHubClientFactory();
        var actor = new EngineActor(factory, new Uri("http://localhost:5000"), "GAME01");

        await actor.StartAsync();

        var call = factory.Client!.Calls.Single();
        Assert.Equal("GAME01", call.Session);
        Assert.Equal("engine", call.Role);
        Assert.Null(call.Name);
    }

    [Fact]
    public async Task Audience_joins_session_and_role_with_name()
    {
        var factory = new FakeHubClientFactory();
        var actor = new AudienceActor(factory, new Uri("http://localhost:5000"), "XYZ", "Alice");

        await actor.StartAsync();

        var call = factory.Client!.Calls.Single();
        Assert.Equal("XYZ", call.Session);
        Assert.Equal("audience", call.Role);
        Assert.Equal("Alice", call.Name);
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
    public List<(string Session, string Role, string? Name)> Calls { get; } = new();

    public FakeHubClient(Uri baseAddress)
    {
        BaseAddress = baseAddress;
    }

    public Task StartAsync(CancellationToken cancellationToken = default)
        => Task.CompletedTask;

    public Task StopAsync(CancellationToken cancellationToken = default)
        => Task.CompletedTask;

    public Task JoinAsync(string session, string role, string? name = null, CancellationToken cancellationToken = default)
    {
        Calls.Add((session, role, name));
        return Task.CompletedTask;
    }

    public Task SubmitAnswerAsync(string session, int choiceIndex, CancellationToken cancellationToken = default)
        => Task.CompletedTask;

    public IDisposable OnGameStateChanged(Action<GameStateSnapshot> handler)
        => new NoopDisposable();

    sealed class NoopDisposable : IDisposable { public void Dispose() { } }
}