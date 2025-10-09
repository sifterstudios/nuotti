using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Nuotti.Backend;
using Nuotti.Backend.Models;
using Nuotti.Backend.Sessions;
using Nuotti.Contracts.V1.Event;
using Nuotti.Contracts.V1.Eventing;
using System.Collections.Concurrent;
using System.Security.Claims;
using Xunit;
namespace Nuotti.Performer.Tests;

public class PerformerJoinInProcTests
{
    sealed class FakeClientProxy : IClientProxy
    {
        public Task SendCoreAsync(string method, object?[] args, CancellationToken cancellationToken = default) => Task.CompletedTask;
    }
    sealed class FakeClients : IHubCallerClients { public IClientProxy Caller => new FakeClientProxy(); public IClientProxy All => throw new NotImplementedException(); public IClientProxy AllExcept(IReadOnlyList<string> excludedConnectionIds) => throw new NotImplementedException(); public IClientProxy Client(string connectionId) => new FakeClientProxy(); public IClientProxy Clients(IReadOnlyList<string> connectionIds) => new FakeClientProxy(); public IClientProxy Group(string groupName) => new FakeClientProxy(); public IClientProxy GroupExcept(string groupName, IReadOnlyList<string> excludedConnectionIds) => new FakeClientProxy(); public IClientProxy Groups(IReadOnlyList<string> groupNames) => new FakeClientProxy(); public IClientProxy Others => new FakeClientProxy(); public IClientProxy OthersInGroup(string groupName) => new FakeClientProxy(); public IClientProxy User(string userId) => new FakeClientProxy(); public IClientProxy Users(IReadOnlyList<string> userIds) => new FakeClientProxy(); }
    sealed class NoopGroupManager : IGroupManager { public Task AddToGroupAsync(string connectionId, string groupName, CancellationToken cancellationToken = default) => Task.CompletedTask; public Task RemoveFromGroupAsync(string connectionId, string groupName, CancellationToken cancellationToken = default) => Task.CompletedTask; }
    sealed class TestContext(string connectionId) : HubCallerContext { public override string ConnectionId { get; } = connectionId; public override string? UserIdentifier => null; public override ClaimsPrincipal? User => null; public override IDictionary<object, object?> Items { get; } = new Dictionary<object, object?>(); public override IFeatureCollection Features { get; } = new FeatureCollection(); public override CancellationToken ConnectionAborted { get; } = CancellationToken.None; public override void Abort() { } }
    sealed class FakeLogStreamer : ILogStreamer { public Task BroadcastAsync(LogEvent evt) => Task.CompletedTask; }
    sealed class CapturingEventBus : IEventBus { public readonly ConcurrentBag<object> Published = new(); public Task PublishAsync<TEvent>(TEvent evt, CancellationToken ct = default) { Published.Add(evt!); return Task.CompletedTask; } public IDisposable Subscribe<TEvent>(Func<TEvent, CancellationToken, Task> handler) => new DummyDisposable(); sealed class DummyDisposable : IDisposable { public void Dispose() { } } }
    sealed class TestableQuizHub(ILogStreamer log, ISessionStore sessions, IEventBus bus) : QuizHub(new NullLogger<QuizHub>(), log, sessions, bus) { public void SetContext(HubCallerContext ctx) => Context = ctx; public void SetGroups(IGroupManager groups) => Groups = groups; public void SetClients(IHubCallerClients clients) => Clients = clients; }

    static InMemorySessionStore CreateSessionStore() => new InMemorySessionStore(Options.Create(new NuottiOptions()));

    [Fact]
    public async Task Join_Performer_registers_role_in_session_store()
    {
        var store = CreateSessionStore();
        var hub = new TestableQuizHub(new FakeLogStreamer(), store, new CapturingEventBus());
        hub.SetContext(new TestContext("perf-conn-1"));
        hub.SetClients(new FakeClients());
        hub.SetGroups(new NoopGroupManager());

        await hub.Join("sessZ", "Performer");

        var counts = store.GetCounts("sessZ");
        Assert.Equal(1, counts.Performer);
        Assert.Equal(0, counts.Audiences);
    }
}