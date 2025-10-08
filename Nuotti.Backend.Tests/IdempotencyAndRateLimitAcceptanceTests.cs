using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.SignalR;
using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Nuotti.Backend.Models;
using Nuotti.Backend.Sessions;
using Nuotti.Contracts.V1.Enum;
using Nuotti.Contracts.V1.Event;
using Nuotti.Contracts.V1.Eventing;
using Nuotti.Contracts.V1.Message.Phase;
using Nuotti.Contracts.V1.Model;
using System.Collections.Concurrent;
using System.Net;
using System.Net.Http.Json;
using System.Security.Claims;
using Xunit;
namespace Nuotti.Backend.Tests;

// Minimal test doubles copied from QuizHubInProcTests to be self-contained
file sealed class FakeClientProxy : IClientProxy
{
    public readonly ConcurrentBag<(string method, object?[] args)> Sent = new ConcurrentBag<(string method, object?[] args)>();
    public Task SendCoreAsync(string method, object?[] args, CancellationToken cancellationToken = default)
    {
        Sent.Add((method, args));
        return Task.CompletedTask;
    }
}

file sealed class FakeClients : IHubCallerClients
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

file sealed class CapturingGroupManager : IGroupManager
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
        return Task.CompletedTask;
    }
}

file sealed class TestContext(string connectionId) : HubCallerContext
{
    public override string ConnectionId { get; } = connectionId;
    public override string? UserIdentifier => null;
    public override ClaimsPrincipal? User => null;
    public override IDictionary<object, object?> Items { get; } = new Dictionary<object, object?>();
    public override IFeatureCollection Features { get; } = new FeatureCollection();
    public override CancellationToken ConnectionAborted { get; } = CancellationToken.None;
    public override void Abort() { }
}

file sealed class FakeLogStreamer : ILogStreamer
{
    public Task BroadcastAsync(LogEvent evt) => Task.CompletedTask;
}

file sealed class CapturingEventBus : IEventBus
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

file sealed class TestableQuizHub(ILogStreamer log, ISessionStore sessions, IEventBus bus) : QuizHub(new NullLogger<QuizHub>(), log, sessions, bus)
{
    public void SetContext(HubCallerContext ctx) => Context = ctx;
    public void SetGroups(IGroupManager groups) => Groups = groups;
    public void SetClients(IHubCallerClients clients) => Clients = clients;
}

public class IdempotencyAndRateLimitAcceptanceTests : IClassFixture<WebApplicationFactory<Program>>
{
    readonly WebApplicationFactory<Program> _factory;

    public IdempotencyAndRateLimitAcceptanceTests(WebApplicationFactory<Program> factory)
    {
        _factory = factory.WithWebHostBuilder(_ => { });
    }

    static StartGame MakeStart(string session, Guid cmdId) => new StartGame
    {
        SessionCode = session,
        IssuedByRole = Role.Performer,
        IssuedById = "test-performer",
        CommandId = cmdId
    };

    [Fact]
    public async Task Duplicate_CommandId_Replayed_Does_Not_DoubleApply()
    {
        var session = "idem-sess-1";
        var client = _factory.CreateClient();

        // Prepare SignalR connection using TestServer handler
        var handler = _factory.Server.CreateHandler();
        var baseAddress = _factory.Server.BaseAddress;
        var receivedCount = 0;
        var tcsFirst = new TaskCompletionSource<GameStateSnapshot>(TaskCreationOptions.RunContinuationsAsynchronously);

        var connection = new HubConnectionBuilder()
            .WithUrl(new Uri(baseAddress, "/hub"), options =>
            {
                options.HttpMessageHandlerFactory = _ => handler;
            })
            .WithAutomaticReconnect()
            .Build();

        connection.On<GameStateSnapshot>("GameStateChanged", snapshot =>
        {
            if (snapshot.SessionCode == session)
            {
                Interlocked.Increment(ref receivedCount);
                tcsFirst.TrySetResult(snapshot);
            }
        });

        await connection.StartAsync();
        await connection.InvokeAsync("Join", session, "projector", null);

        var cmdId = Guid.Parse("aaaaaaaa-aaaa-4aaa-aaaa-aaaaaaaaaaaa");
        // Send StartGame twice with the same CommandId
        var resp1 = await client.PostAsJsonAsync($"/v1/message/phase/start-game/{session}", MakeStart(session, cmdId));
        var resp2 = await client.PostAsJsonAsync($"/v1/message/phase/start-game/{session}", MakeStart(session, cmdId));

        Assert.Equal(HttpStatusCode.Accepted, resp1.StatusCode);
        Assert.Equal(HttpStatusCode.Accepted, resp2.StatusCode);

        // Wait for first broadcast
        var first = await Task.WhenAny(tcsFirst.Task, Task.Delay(TimeSpan.FromSeconds(5)));
        Assert.True(ReferenceEquals(first, tcsFirst.Task), "Did not receive first GameStateChanged in time");
        var snapshot = await tcsFirst.Task;
        Assert.Equal(Phase.Start, snapshot.Phase);

        // Ensure no second broadcast within a short delay
        await Task.Delay(300);
        Assert.Equal(1, receivedCount);

        await connection.DisposeAsync();
    }

    [Fact]
    public async Task Rapid_SubmitAnswer_Attempts_Return_429_From_Hub()
    {
        var store = new InMemorySessionStore(Options.Create(new NuottiOptions()));
        var bus = new CapturingEventBus();
        var hub = new TestableQuizHub(new FakeLogStreamer(), store, bus);
        var clients = new FakeClients();
        var groups = new CapturingGroupManager();
        hub.SetClients(clients);
        hub.SetGroups(groups);

        // Audience joins
        var audienceCtx = new TestContext("aud-rate-1");
        hub.SetContext(audienceCtx);
        await hub.Join("rateSess", "Audience", name: "Bob");

        // First SubmitAnswer allowed
        await hub.SubmitAnswer("rateSess", 1);
        // Immediate second should be rate-limited
        await hub.SubmitAnswer("rateSess", 2);

        // Assert a Problem was sent with Status 429
        var problemSent = clients.CallerProxy.Sent.Where(x => x.method == "Problem")
            .Select(x => x.args.FirstOrDefault() as NuottiProblem)
            .FirstOrDefault(p => p is not null && p.Status == 429);
        Assert.NotNull(problemSent);

        // Only one AnswerSubmitted event should be published
        var publishedCount = bus.Published.Count(e => e is AnswerSubmitted);
        Assert.Equal(1, publishedCount);
    }
}
