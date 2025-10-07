using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Nuotti.Backend.Models;
using Nuotti.Backend.Sessions;
using Nuotti.Contracts.V1.Event;
using Nuotti.Contracts.V1.Eventing;
using System.Collections.Concurrent;
using System.Security.Claims;
using Xunit;
namespace Nuotti.Backend.Tests;

public class QuizHubInProcTests
{
    sealed class FakeClientProxy : IClientProxy
    {
        public readonly ConcurrentBag<(string method, object?[] args)> Sent = new ConcurrentBag<(string method, object?[] args)>();
        public Task SendCoreAsync(string method, object?[] args, CancellationToken cancellationToken = default)
        {
            Sent.Add((method, args));
            return Task.CompletedTask;
        }
    }

    sealed class FakeClients : IHubCallerClients
    {
        public readonly FakeClientProxy CallerProxy = new FakeClientProxy();
        public IClientProxy Caller => CallerProxy;
        public IClientProxy All => throw new NotImplementedException();
        public IClientProxy AllExcept(IReadOnlyList<string> excludedConnectionIds) => throw new NotImplementedException();
        public IClientProxy Client(string connectionId) => new FakeClientProxy();
        public IClientProxy Clients(IReadOnlyList<string> connectionIds) => new FakeClientProxy();
        public IClientProxy Group(string groupName) => new FakeClientProxy();
        public IClientProxy GroupExcept(string groupName, IReadOnlyList<string> excludedConnectionIds) => new FakeClientProxy();
        public IClientProxy Groups(IReadOnlyList<string> groupNames) => new FakeClientProxy();
        public IClientProxy Others => new FakeClientProxy();
        public IClientProxy OthersInGroup(string groupName) => new FakeClientProxy();
        public IClientProxy User(string userId) => new FakeClientProxy();
        public IClientProxy Users(IReadOnlyList<string> userIds) => new FakeClientProxy();
    }

    sealed class CapturingGroupManager : IGroupManager
    {
        public readonly ConcurrentDictionary<string, ConcurrentBag<string>> Groups = new ConcurrentDictionary<string, ConcurrentBag<string>>();
        public Task AddToGroupAsync(string connectionId, string groupName, CancellationToken cancellationToken = default)
        {
            var bag = Groups.GetOrAdd(groupName, _ => new ConcurrentBag<string>());
            bag.Add(connectionId);
            return Task.CompletedTask;
        }
        public Task RemoveFromGroupAsync(string connectionId, string groupName, CancellationToken cancellationToken = default)
        {
            // no-op for tests
            return Task.CompletedTask;
        }
    }

    sealed class TestContext(string connectionId) : HubCallerContext
    {
        public override string ConnectionId
        {
            get;
        } = connectionId;
        public override string? UserIdentifier => null;
        public override ClaimsPrincipal? User => null;
        public override IDictionary<object, object?> Items
        {
            get;
        } = new Dictionary<object, object?>();
        public override IFeatureCollection Features { get; } = new FeatureCollection();
        public override CancellationToken ConnectionAborted { get; } = CancellationToken.None;
        public override void Abort() { }
    }

    sealed class FakeLogStreamer : ILogStreamer
    {
        public Task BroadcastAsync(LogEvent evt) => Task.CompletedTask;
    }

    sealed class CapturingEventBus : IEventBus
    {
        public readonly ConcurrentBag<object> Published = new ConcurrentBag<object>();
        public Task PublishAsync<TEvent>(TEvent evt, CancellationToken ct = default)
        {
            Published.Add(evt!);
            return Task.CompletedTask;
        }
        public IDisposable Subscribe<TEvent>(Func<TEvent, CancellationToken, Task> handler) => new DummyDisposable();
        sealed class DummyDisposable : IDisposable { public void Dispose() { } }
    }

    sealed class TestableQuizHub(ILogStreamer log, ISessionStore sessions, IEventBus bus) : QuizHub(new NullLogger<QuizHub>(), log, sessions, bus)
    {
        public void SetContext(HubCallerContext ctx) => Context = ctx;
        public void SetGroups(IGroupManager groups) => Groups = groups;
        public void SetClients(IHubCallerClients clients) => Clients = clients;
    }

    static InMemorySessionStore CreateSessionStore() => new InMemorySessionStore(Options.Create(new NuottiOptions()));

    [Fact]
    public async Task Join_Adds_To_Session_And_Role_Groups()
    {
        var store = CreateSessionStore();
        var hub = new TestableQuizHub(new FakeLogStreamer(), store, new CapturingEventBus());
        var ctx = new TestContext("conn-1");
        var clients = new FakeClients();
        var groups = new CapturingGroupManager();
        hub.SetContext(ctx);
        hub.SetClients(clients);
        hub.SetGroups(groups);

        await hub.Join("sessA", "Audience", name: "Alice");

        Assert.True(groups.Groups.TryGetValue("sessA", out var sessGroup) && sessGroup.Contains("conn-1"));
        Assert.True(groups.Groups.TryGetValue("sessA:audience", out var roleGroup) && roleGroup.Contains("conn-1"));

        var counts = store.GetCounts("sessA");
        Assert.Equal(0, counts.Performer);
        Assert.Equal(1, counts.Audiences);
    }

    [Fact]
    public async Task SubmitAnswer_blocks_non_audience_and_allows_audience()
    {
        var store = CreateSessionStore();
        var bus = new CapturingEventBus();
        var hub = new TestableQuizHub(new FakeLogStreamer(), store, bus);
        var clients = new FakeClients();
        var groups = new CapturingGroupManager();
        hub.SetClients(clients);
        hub.SetGroups(groups);

        // Performer joins
        var performerCtx = new TestContext("perf-1");
        hub.SetContext(performerCtx);
        await hub.Join("sessB", "Performer");
        await hub.SubmitAnswer("sessB", 1);
        // Expect a Problem sent to Caller
        Assert.Contains(clients.CallerProxy.Sent, x => x.method == "Problem");

        // Audience joins and submits an answer
        var audienceCtx = new TestContext("aud-1");
        clients.CallerProxy.Sent.Clear();
        hub.SetContext(audienceCtx);
        await hub.Join("sessB", "Audience", name: "Bob");
        await hub.SubmitAnswer("sessB", 2);
        Assert.Contains(bus.Published, e => e is AnswerSubmitted { ChoiceIndex: 2, SessionCode: "sessB" });
    }
}
