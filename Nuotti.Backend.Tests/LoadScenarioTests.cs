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
namespace Nuotti.Backend.Tests;

public class LoadScenarioTests
{
    // Minimal in-proc hub harness (copied from QuizHubInProcTests with only members needed here)
    sealed class FakeClientProxy : IClientProxy
    {
        public readonly ConcurrentBag<(string method, object?[] args)> Sent = new();
        public Task SendCoreAsync(string method, object?[] args, CancellationToken cancellationToken = default)
        {
            Sent.Add((method, args));
            return Task.CompletedTask;
        }
    }
    sealed class FakeClients : IHubCallerClients
    {
        public readonly FakeClientProxy CallerProxy = new();
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
        public readonly ConcurrentDictionary<string, ConcurrentBag<string>> Groups = new();
        public Task AddToGroupAsync(string connectionId, string groupName, CancellationToken cancellationToken = default)
        {
            var bag = Groups.GetOrAdd(groupName, _ => new ConcurrentBag<string>());
            bag.Add(connectionId);
            return Task.CompletedTask;
        }
        public Task RemoveFromGroupAsync(string connectionId, string groupName, CancellationToken cancellationToken = default) => Task.CompletedTask;
    }
    sealed class TestContext(string connectionId) : HubCallerContext
    {
        public override string ConnectionId { get; } = connectionId;
        public override string? UserIdentifier => null;
        public override ClaimsPrincipal? User => null;
        public override IDictionary<object, object?> Items { get; } = new Dictionary<object, object?>();
        public override IFeatureCollection Features { get; } = new FeatureCollection();
        public override CancellationToken ConnectionAborted { get; } = CancellationToken.None;
        public override void Abort() { }
    }
    sealed class FakeLogStreamer : ILogStreamer { public Task BroadcastAsync(LogEvent evt) => Task.CompletedTask; }
    sealed class CapturingEventBus : IEventBus
    {
        public readonly ConcurrentBag<object> Published = new();
        public Task PublishAsync<TEvent>(TEvent evt, CancellationToken ct = default) { Published.Add(evt!); return Task.CompletedTask; }
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

    [Theory]
    [InlineData(200)]
    [InlineData(500)]
    public async Task Backend_responsive_under_load_and_error_rate_below_1_percent(int audiences)
    {
        // Arrange a shared store and bus to simulate a real backend state under concurrent access
        var store = CreateSessionStore();
        var bus = new CapturingEventBus();
        var groups = new CapturingGroupManager();
        var session = "LOAD01";

        var errors = new ConcurrentBag<System.Exception>();

        // Act: run concurrent join+submit cycles
        await Parallel.ForEachAsync(Enumerable.Range(0, audiences), async (i, ct) =>
        {
            try
            {
                var hub = new TestableQuizHub(new FakeLogStreamer(), store, bus);
                var clients = new FakeClients();
                hub.SetClients(clients);
                hub.SetGroups(groups);
                var ctx = new TestContext($"load-aud-{i}");
                hub.SetContext(ctx);

                await hub.Join(session, "Audience", name: $"A-{i}");
                await hub.SubmitAnswer(session, choiceIndex: i % 4);
            }
            catch (System.Exception ex)
            {
                errors.Add(ex);
            }
        });

        // Assert: error rate < 1%
        double errorRate = audiences == 0 ? 0 : (double)errors.Count / audiences;
        Assert.True(errorRate < 0.01, $"Error rate {errorRate:P2} exceeded 1%. errors={errors.Count} of {audiences}");
    }
}
