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
using Nuotti.Contracts.V1.Message;
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

public class IdempotencyAndRateLimitAcceptanceTests : IClassFixture<WebApplicationFactory<QuizHub>>
{
    readonly WebApplicationFactory<QuizHub> _factory;

    public IdempotencyAndRateLimitAcceptanceTests(WebApplicationFactory<QuizHub> factory)
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

    [Fact]
    public async Task Rapid_RequestPlay_Attempts_Return_429_From_Hub()
    {
        var store = new InMemorySessionStore(Options.Create(new NuottiOptions()));
        var bus = new CapturingEventBus();
        var hub = new TestableQuizHub(new FakeLogStreamer(), store, bus);
        var clients = new FakeClients();
        var groups = new CapturingGroupManager();
        hub.SetClients(clients);
        hub.SetGroups(groups);

        // Audience joins
        var audienceCtx = new TestContext("aud-play-1");
        hub.SetContext(audienceCtx);
        await hub.Join("playSess", "Audience", name: "Alice");

        var cmd = new PlayTrack("https://example.com/track.mp3")
        {
            SessionCode = "playSess",
            IssuedByRole = Role.Audience,
            IssuedById = "aud-play-1"
        };

        // First RequestPlay allowed
        await hub.RequestPlay("playSess", cmd);
        // Immediate second should be rate-limited (2 second window)
        await hub.RequestPlay("playSess", cmd);

        // Assert a Problem was sent with Status 429
        var problemSent = clients.CallerProxy.Sent.Where(x => x.method == "Problem")
            .Select(x => x.args.FirstOrDefault() as NuottiProblem)
            .FirstOrDefault(p => p is not null && p.Status == 429);
        Assert.NotNull(problemSent);
        Assert.Equal("Too Many Requests", problemSent.Title);
        Assert.Contains("too quickly", problemSent.Detail, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task SubmitAnswer_After_Window_Expires_Is_Allowed()
    {
        var store = new InMemorySessionStore(Options.Create(new NuottiOptions()));
        var bus = new CapturingEventBus();
        var hub = new TestableQuizHub(new FakeLogStreamer(), store, bus);
        var clients = new FakeClients();
        var groups = new CapturingGroupManager();
        hub.SetClients(clients);
        hub.SetGroups(groups);

        // Audience joins
        var audienceCtx = new TestContext("aud-window-1");
        hub.SetContext(audienceCtx);
        await hub.Join("windowSess", "Audience", name: "Charlie");

        // First SubmitAnswer allowed
        await hub.SubmitAnswer("windowSess", 1);
        // Immediate second should be rate-limited
        await hub.SubmitAnswer("windowSess", 2);

        // Verify rate limit was applied
        var rateLimited = clients.CallerProxy.Sent.Any(x => 
            x.method == "Problem" && 
            x.args[0] is NuottiProblem p && p.Status == 429);
        Assert.True(rateLimited);

        // Clear the sent messages
        clients.CallerProxy.Sent.Clear();

        // Wait for the 500ms window to expire
        await Task.Delay(TimeSpan.FromMilliseconds(600));

        // Should be allowed again after window expires
        await hub.SubmitAnswer("windowSess", 3);

        // Should not receive a rate limit error
        var rateLimitedAfter = clients.CallerProxy.Sent.Any(x => 
            x.method == "Problem" && 
            x.args[0] is NuottiProblem p && p.Status == 429);
        Assert.False(rateLimitedAfter);

        // Should have published the answer
        var publishedCount = bus.Published.Count(e => e is AnswerSubmitted a && a.ChoiceIndex == 3);
        Assert.Equal(1, publishedCount);
    }

    [Fact]
    public async Task Different_Connections_Can_Submit_Answers_Independently()
    {
        var store = new InMemorySessionStore(Options.Create(new NuottiOptions()));
        var bus = new CapturingEventBus();
        var hub = new TestableQuizHub(new FakeLogStreamer(), store, bus);
        var clients = new FakeClients();
        var groups = new CapturingGroupManager();
        hub.SetClients(clients);
        hub.SetGroups(groups);

        // First audience joins
        var audience1Ctx = new TestContext("aud-indep-1");
        hub.SetContext(audience1Ctx);
        await hub.Join("indepSess", "Audience", name: "Dave");
        await hub.SubmitAnswer("indepSess", 1);

        // Second audience joins (different connection)
        var audience2Ctx = new TestContext("aud-indep-2");
        hub.SetContext(audience2Ctx);
        await hub.Join("indepSess", "Audience", name: "Eve");
        await hub.SubmitAnswer("indepSess", 2);

        // Both should have published answers (rate limiting is per-connection)
        var publishedAnswers = bus.Published.OfType<AnswerSubmitted>().ToList();
        Assert.Equal(2, publishedAnswers.Count);
        Assert.Contains(publishedAnswers, a => a.AudienceId == "aud-indep-1" && a.ChoiceIndex == 1);
        Assert.Contains(publishedAnswers, a => a.AudienceId == "aud-indep-2" && a.ChoiceIndex == 2);
    }

    [Fact]
    public async Task Two_identical_commands_result_in_single_state_change()
    {
        var session = "idem-sess-2";
        var client = _factory.CreateClient();
        var cmdId = Guid.Parse("bbbbbbbb-bbbb-4bbb-bbbb-bbbbbbbbbbbb");

        // Get initial state (may be 404 if session doesn't exist yet)
        var initialStatus = await client.GetAsync($"/status/{session}");
        Phase initialPhase = Phase.Lobby;
        if (initialStatus.StatusCode == HttpStatusCode.OK)
        {
            var initialState = await initialStatus.Content.ReadFromJsonAsync<GameStateSnapshot>(Nuotti.Contracts.V1.ContractsJson.RestOptions);
            if (initialState != null)
            {
                initialPhase = initialState.Phase;
            }
        }

        // Send StartGame command twice with same CommandId
        var resp1 = await client.PostAsJsonAsync($"/v1/message/phase/start-game/{session}", MakeStart(session, cmdId));
        var resp2 = await client.PostAsJsonAsync($"/v1/message/phase/start-game/{session}", MakeStart(session, cmdId));

        Assert.Equal(HttpStatusCode.Accepted, resp1.StatusCode);
        Assert.Equal(HttpStatusCode.Accepted, resp2.StatusCode);

        // Wait a bit for state to update
        await Task.Delay(500);

        // Check final state - should only have changed once
        var finalStatus = await client.GetAsync($"/status/{session}");
        Assert.Equal(HttpStatusCode.OK, finalStatus.StatusCode);
        var finalState = await finalStatus.Content.ReadFromJsonAsync<GameStateSnapshot>(Nuotti.Contracts.V1.ContractsJson.RestOptions);
        Assert.NotNull(finalState);

        // If initial phase was Lobby, it should be Start now (only one transition)
        if (initialPhase == Phase.Lobby)
        {
            Assert.Equal(Phase.Start, finalState.Phase);
        }
    }
}
