using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Nuotti.Backend.Commands;
using Nuotti.Backend.Idempotency;
using Nuotti.Backend.Models;
using Nuotti.Backend.Participants;
using Nuotti.Backend.Sessions;
using Nuotti.Contracts.V1.Event;
using Nuotti.Contracts.V1.Eventing;
using System.Collections.Concurrent;
using System.Security.Claims;
namespace Nuotti.Backend.Tests.TestSupport;

/// <summary>
/// One in-process hub harness for the whole test project. This used to be copied verbatim into
/// every file that needed to drive QuizHub — five copies, one of them carrying the comment
/// "copied from QuizHubInProcTests".
/// </summary>
public sealed class FakeClientProxy : IClientProxy
{
    public readonly ConcurrentBag<(string method, object?[] args)> Sent = new();

    public Task SendCoreAsync(string method, object?[] args, CancellationToken cancellationToken = default)
    {
        Sent.Add((method, args));
        return Task.CompletedTask;
    }
}

public sealed class FakeClients : IHubCallerClients
{
    public readonly FakeClientProxy CallerProxy = new();

    /// <summary>Group proxies are remembered so a test can assert what a given group received.</summary>
    public readonly ConcurrentDictionary<string, FakeClientProxy> GroupProxies = new();

    public IClientProxy Caller => CallerProxy;

    public FakeClientProxy GroupProxy(string groupName)
        => GroupProxies.GetOrAdd(groupName, _ => new FakeClientProxy());

    public IClientProxy Group(string groupName) => GroupProxy(groupName);
    public IClientProxy All => throw new NotImplementedException();
    public IClientProxy AllExcept(IReadOnlyList<string> excludedConnectionIds) => throw new NotImplementedException();
    public IClientProxy Client(string connectionId) => new FakeClientProxy();
    public IClientProxy Clients(IReadOnlyList<string> connectionIds) => new FakeClientProxy();
    public IClientProxy GroupExcept(string groupName, IReadOnlyList<string> excludedConnectionIds) => new FakeClientProxy();
    public IClientProxy Groups(IReadOnlyList<string> groupNames) => new FakeClientProxy();
    public IClientProxy Others => new FakeClientProxy();
    public IClientProxy OthersInGroup(string groupName) => new FakeClientProxy();
    public IClientProxy User(string userId) => new FakeClientProxy();
    public IClientProxy Users(IReadOnlyList<string> userIds) => new FakeClientProxy();
}

public sealed class CapturingGroupManager : IGroupManager
{
    public readonly ConcurrentDictionary<string, ConcurrentBag<string>> Groups = new();

    public Task AddToGroupAsync(string connectionId, string groupName, CancellationToken cancellationToken = default)
    {
        Groups.GetOrAdd(groupName, _ => new ConcurrentBag<string>()).Add(connectionId);
        return Task.CompletedTask;
    }

    public Task RemoveFromGroupAsync(string connectionId, string groupName, CancellationToken cancellationToken = default)
        => Task.CompletedTask;
}

public sealed class TestContext(string connectionId) : HubCallerContext
{
    public override string ConnectionId { get; } = connectionId;
    public override string? UserIdentifier => null;
    public override ClaimsPrincipal? User => null;
    public override IDictionary<object, object?> Items { get; } = new Dictionary<object, object?>();
    public override IFeatureCollection Features { get; } = new FeatureCollection();
    public override CancellationToken ConnectionAborted { get; } = CancellationToken.None;
    public override void Abort() { }
}

public sealed class FakeLogStreamer : ILogStreamer
{
    public readonly ConcurrentBag<LogEvent> Events = new();

    public Task BroadcastAsync(LogEvent evt)
    {
        Events.Add(evt);
        return Task.CompletedTask;
    }
}

/// <summary>
/// Records what the processor published without running any subscriber.
/// </summary>
public sealed class CapturingEventBus : IEventBus
{
    public readonly ConcurrentBag<object> Published = new();

    public Task PublishAsync<TEvent>(TEvent evt, CancellationToken ct = default)
    {
        Published.Add(evt!);
        return Task.CompletedTask;
    }

    public IDisposable Subscribe<TEvent>(Func<TEvent, CancellationToken, Task> handler) => new Noop();

    sealed class Noop : IDisposable
    {
        public void Dispose() { }
    }
}

/// <summary>
/// QuizHub with its Context, Groups and Clients settable, which the base class does not allow.
/// </summary>
public sealed class TestableQuizHub(
    ILogStreamer log,
    ISessionStore sessions,
    ISessionCommandProcessor processor,
    IParticipantIdentityStore participants)
    : QuizHub(new NullLogger<QuizHub>(), log, sessions, processor, participants)
{
    public void SetContext(HubCallerContext ctx) => Context = ctx;
    public void SetGroups(IGroupManager groups) => Groups = groups;
    public void SetClients(IHubCallerClients clients) => Clients = clients;
}

public static class Harness
{
    public static InMemorySessionStore SessionStore()
        => new(Options.Create(new NuottiOptions()), new InMemoryGameStateStore());

    public static InMemoryIdempotencyStore IdempotencyStore()
        => new(Options.Create(new NuottiOptions()));

    public static InMemoryParticipantIdentityStore Participants() => new();

    /// <summary>
    /// A hub wired to a real processor over in-memory stores, publishing to <paramref name="bus"/>.
    /// </summary>
    public static TestableQuizHub Hub(ISessionStore sessions, CapturingEventBus bus)
        => Hub(sessions, bus, out _);

    /// <summary>
    /// As above, exposing the game state store so a test can put the session into the phase the
    /// command under test requires.
    /// </summary>
    public static TestableQuizHub Hub(ISessionStore sessions, CapturingEventBus bus, out IGameStateStore state)
    {
        state = new InMemoryGameStateStore();
        return new TestableQuizHub(new FakeLogStreamer(), sessions, new SessionCommandProcessor(
            state,
            IdempotencyStore(),
            bus,
            NullLogger<SessionCommandProcessor>.Instance), Participants());
    }

    /// <summary>
    /// A real processor over in-memory stores and a capturing bus. No host, no SignalR.
    /// </summary>
    public static SessionCommandProcessor Processor(
        out IGameStateStore state,
        out CapturingEventBus bus,
        IIdempotencyStore? idempotency = null)
    {
        state = new InMemoryGameStateStore();
        bus = new CapturingEventBus();
        return new SessionCommandProcessor(
            state,
            idempotency ?? IdempotencyStore(),
            bus,
            NullLogger<SessionCommandProcessor>.Instance);
    }
}
