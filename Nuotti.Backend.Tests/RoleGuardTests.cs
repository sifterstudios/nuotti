using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Nuotti.Backend.Models;
using Nuotti.Backend.Sessions;
using Nuotti.Contracts.V1;
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

namespace Nuotti.Backend.Tests;

// Reuse test doubles from QuizHubInProcTests
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
    public IClientProxy GroupExcept(string groupName, IReadOnlyList<string> excludedConnectionIds) => throw new NotImplementedException();
    public IClientProxy Groups(IReadOnlyList<string> groupNames) => new FakeClientProxy();
    public IClientProxy Others => new FakeClientProxy();
    public IClientProxy OthersInGroup(string groupName) => new FakeClientProxy();
    public IClientProxy User(string userId) => throw new NotImplementedException();
    public IClientProxy Users(IReadOnlyList<string> userIds) => throw new NotImplementedException();
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

public class RoleGuardTests : IClassFixture<WebApplicationFactory<QuizHub>>
{
    readonly WebApplicationFactory<QuizHub> _factory;

    public RoleGuardTests(WebApplicationFactory<QuizHub> factory)
    {
        _factory = factory.WithWebHostBuilder(_ => { });
    }

    [Fact]
    public async Task StartGame_Audience_Role_Returns403_WithUnauthorizedRole()
    {
        var session = "role-test-1";
        var client = _factory.CreateClient();

        var payload = new StartGame
        {
            SessionCode = session,
            IssuedByRole = Role.Audience,
            IssuedById = "aud-1",
            CommandId = Guid.NewGuid()
        };

        var resp = await client.PostAsJsonAsync($"/v1/message/phase/start-game/{session}", payload);
        Assert.Equal(HttpStatusCode.Forbidden, resp.StatusCode);
        
        var problem = await resp.Content.ReadFromJsonAsync<NuottiProblem>(ContractsJson.RestOptions);
        Assert.NotNull(problem);
        Assert.Equal(403, problem.Status);
        Assert.Equal(ReasonCode.UnauthorizedRole, problem.Reason);
        Assert.Equal("issuedByRole", problem.Field);
    }

    [Fact]
    public async Task StartGame_Projector_Role_Returns403_WithUnauthorizedRole()
    {
        var session = "role-test-2";
        var client = _factory.CreateClient();

        var payload = new StartGame
        {
            SessionCode = session,
            IssuedByRole = Role.Projector,
            IssuedById = "proj-1",
            CommandId = Guid.NewGuid()
        };

        var resp = await client.PostAsJsonAsync($"/v1/message/phase/start-game/{session}", payload);
        Assert.Equal(HttpStatusCode.Forbidden, resp.StatusCode);
        
        var problem = await resp.Content.ReadFromJsonAsync<NuottiProblem>(ContractsJson.RestOptions);
        Assert.NotNull(problem);
        Assert.Equal(ReasonCode.UnauthorizedRole, problem.Reason);
    }

    [Fact]
    public async Task StartGame_Engine_Role_Returns403_WithUnauthorizedRole()
    {
        var session = "role-test-3";
        var client = _factory.CreateClient();

        var payload = new StartGame
        {
            SessionCode = session,
            IssuedByRole = Role.Engine,
            IssuedById = "engine-1",
            CommandId = Guid.NewGuid()
        };

        var resp = await client.PostAsJsonAsync($"/v1/message/phase/start-game/{session}", payload);
        Assert.Equal(HttpStatusCode.Forbidden, resp.StatusCode);
        
        var problem = await resp.Content.ReadFromJsonAsync<NuottiProblem>(ContractsJson.RestOptions);
        Assert.NotNull(problem);
        Assert.Equal(ReasonCode.UnauthorizedRole, problem.Reason);
    }

    [Fact]
    public async Task LockAnswers_Audience_Role_Returns403()
    {
        var session = "role-test-4";
        var client = _factory.CreateClient();

        var payload = new LockAnswers
        {
            SessionCode = session,
            IssuedByRole = Role.Audience,
            IssuedById = "aud-1",
            CommandId = Guid.NewGuid()
        };

        var resp = await client.PostAsJsonAsync($"/v1/message/phase/lock-answers/{session}", payload);
        Assert.Equal(HttpStatusCode.Forbidden, resp.StatusCode);
    }

    [Fact]
    public async Task RevealAnswer_Projector_Role_Returns403()
    {
        var session = "role-test-5";
        var client = _factory.CreateClient();

        var payload = new RevealAnswer(new SongRef(new SongId("song-1"), "Test", "Artist"), 0)
        {
            SessionCode = session,
            IssuedByRole = Role.Projector,
            IssuedById = "proj-1",
            CommandId = Guid.NewGuid()
        };

        var resp = await client.PostAsJsonAsync($"/v1/message/phase/reveal-answer/{session}", payload);
        Assert.Equal(HttpStatusCode.Forbidden, resp.StatusCode);
    }

    [Fact]
    public async Task Hub_SubmitAnswer_Performer_Role_ReturnsProblem()
    {
        var store = new InMemorySessionStore(Options.Create(new NuottiOptions()));
        var bus = new CapturingEventBus();
        var hub = new TestableQuizHub(new FakeLogStreamer(), store, bus);
        var clients = new FakeClients();
        var groups = new CapturingGroupManager();
        hub.SetClients(clients);
        hub.SetGroups(groups);

        // Performer joins
        var performerCtx = new TestContext("perf-1");
        hub.SetContext(performerCtx);
        await hub.Join("role-sess-1", "Performer");

        // Performer tries to submit answer (should be blocked)
        await hub.SubmitAnswer("role-sess-1", 1);

        // Expect a Problem sent to Caller with 422 status
        var problemSent = clients.CallerProxy.Sent
            .Where(x => x.method == "Problem")
            .Select(x => x.args.FirstOrDefault() as NuottiProblem)
            .FirstOrDefault(p => p is not null);

        Assert.NotNull(problemSent);
        Assert.Equal(422, problemSent.Status);
        Assert.Equal(ReasonCode.InvalidStateTransition, problemSent.Reason);
    }

    [Fact]
    public async Task Hub_SubmitAnswer_Projector_Role_ReturnsProblem()
    {
        var store = new InMemorySessionStore(Options.Create(new NuottiOptions()));
        var bus = new CapturingEventBus();
        var hub = new TestableQuizHub(new FakeLogStreamer(), store, bus);
        var clients = new FakeClients();
        var groups = new CapturingGroupManager();
        hub.SetClients(clients);
        hub.SetGroups(groups);

        // Projector joins
        var projectorCtx = new TestContext("proj-1");
        hub.SetContext(projectorCtx);
        await hub.Join("role-sess-2", "Projector");

        // Projector tries to submit answer (should be blocked)
        await hub.SubmitAnswer("role-sess-2", 1);

        // Expect a Problem sent
        var problemSent = clients.CallerProxy.Sent
            .Where(x => x.method == "Problem")
            .Select(x => x.args.FirstOrDefault() as NuottiProblem)
            .FirstOrDefault(p => p is not null);

        Assert.NotNull(problemSent);
        Assert.Equal(422, problemSent.Status);
    }

    [Fact]
    public async Task Hub_SubmitAnswer_Engine_Role_ReturnsProblem()
    {
        var store = new InMemorySessionStore(Options.Create(new NuottiOptions()));
        var bus = new CapturingEventBus();
        var hub = new TestableQuizHub(new FakeLogStreamer(), store, bus);
        var clients = new FakeClients();
        var groups = new CapturingGroupManager();
        hub.SetClients(clients);
        hub.SetGroups(groups);

        // Engine joins (if supported)
        var engineCtx = new TestContext("engine-1");
        hub.SetContext(engineCtx);
        await hub.Join("role-sess-3", "Engine");

        // Engine tries to submit answer (should be blocked)
        await hub.SubmitAnswer("role-sess-3", 1);

        // Expect a Problem sent
        var problemSent = clients.CallerProxy.Sent
            .Where(x => x.method == "Problem")
            .Select(x => x.args.FirstOrDefault() as NuottiProblem)
            .FirstOrDefault(p => p is not null);

        Assert.NotNull(problemSent);
    }

    [Fact]
    public async Task REST_PlayTrack_Audience_Role_Returns403()
    {
        var session = "role-test-6";
        var client = _factory.CreateClient();

        var payload = new PlayTrack("file://test.mp3")
        {
            SessionCode = session,
            IssuedByRole = Role.Audience,
            IssuedById = "aud-1",
            CommandId = Guid.NewGuid()
        };

        var resp = await client.PostAsJsonAsync($"/api/play/{session}", payload);
        Assert.Equal(HttpStatusCode.Forbidden, resp.StatusCode);
        
        var problem = await resp.Content.ReadFromJsonAsync<NuottiProblem>(ContractsJson.RestOptions);
        Assert.NotNull(problem);
        Assert.Equal(ReasonCode.UnauthorizedRole, problem.Reason);
    }

    [Fact]
    public async Task REST_PlayTrack_Projector_Role_Returns403()
    {
        var session = "role-test-7";
        var client = _factory.CreateClient();

        var payload = new PlayTrack("file://test.mp3")
        {
            SessionCode = session,
            IssuedByRole = Role.Projector,
            IssuedById = "proj-1",
            CommandId = Guid.NewGuid()
        };

        var resp = await client.PostAsJsonAsync($"/api/play/{session}", payload);
        Assert.Equal(HttpStatusCode.Forbidden, resp.StatusCode);
    }

    [Fact]
    public async Task REST_GiveHint_Audience_Role_Returns403()
    {
        var session = "role-test-8";
        var client = _factory.CreateClient();

        var payload = new GiveHint(new Hint(0, "Hint text", null, new SongId("song-1")))
        {
            SessionCode = session,
            IssuedByRole = Role.Audience,
            IssuedById = "aud-1",
            CommandId = Guid.NewGuid()
        };

        var resp = await client.PostAsJsonAsync($"/v1/message/phase/give-hint/{session}", payload);
        Assert.Equal(HttpStatusCode.Forbidden, resp.StatusCode);
    }
}

