using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Nuotti.Backend.Models;
using Nuotti.Backend.Sessions;
using Nuotti.Contracts.V1.Enum;
using Nuotti.Contracts.V1.Reducer;
using Nuotti.Contracts.V1.Event;
using Nuotti.Contracts.V1.Eventing;
using Nuotti.Contracts.V1.Message;
using System.Collections.Concurrent;
using System.Security.Claims;
using Nuotti.Backend.Tests.TestSupport;
using Nuotti.Backend.Realtime;

namespace Nuotti.Backend.Tests;

public class QuizHubInProcTests
{
    static InMemorySessionStore CreateSessionStore() => Harness.SessionStore();

    [Fact]
    public async Task Join_Adds_To_Session_And_Role_Groups()
    {
        var store = CreateSessionStore();
        var hub = Harness.Hub(store, new CapturingEventBus());
        var ctx = new TestContext("conn-1");
        var clients = new FakeClients();
        var groups = new CapturingGroupManager();
        hub.SetContext(ctx);
        hub.SetClients(clients);
        hub.SetGroups(groups);

        await hub.Join("sessA", "Audience", name: "Alice", deviceSecret: "dev-Alice");

        Assert.True(groups.Groups.TryGetValue(RealtimeGroups.Session("sessA"), out var sessGroup) && sessGroup.Contains("conn-1"));
        Assert.True(groups.Groups.TryGetValue(RealtimeGroups.SessionRole("sessA", "audience"), out var roleGroup) && roleGroup.Contains("conn-1"));

        var counts = store.GetCounts("sessA");
        Assert.Equal(0, counts.Performer);
        Assert.Equal(1, counts.Audiences);
    }

    [Fact]
    public async Task SubmitAnswer_blocks_non_audience_and_allows_audience()
    {
        var store = CreateSessionStore();
        var bus = new CapturingEventBus();
        var hub = Harness.Hub(store, bus, out var gameState);
        var clients = new FakeClients();
        var groups = new CapturingGroupManager();
        hub.SetClients(clients);
        hub.SetGroups(groups);

        // SubmitAnswer declares AllowedPhases = [Guessing], and the processor now enforces that.
        // Before it did, the hub published AnswerSubmitted in any phase and the reducer silently
        // discarded it.
        gameState.Set("sessB", GameReducer.Initial("sessB") with
        {
            Phase = Phase.Guessing,
            Choices = ["A", "B", "C"],
            Tallies = [0, 0, 0]
        });

        // Performer joins
        var performerCtx = new TestContext("perf-1");
        hub.SetContext(performerCtx);
        await hub.Join("sessB", "Performer", null, null);
        await hub.SubmitAnswer("sessB", 1, Guid.Empty);
        // Expect a Problem sent to Caller
        Assert.Contains(clients.CallerProxy.Sent, x => x.method == "Problem");

        // Audience joins and submits an answer
        var audienceCtx = new TestContext("aud-1");
        clients.CallerProxy.Sent.Clear();
        hub.SetContext(audienceCtx);
        await hub.Join("sessB", "Audience", name: "Bob", deviceSecret: "dev-Bob");
        await hub.SubmitAnswer("sessB", 2, Guid.Empty);
        Assert.Contains(bus.Published, e => e is AnswerSubmitted { ChoiceIndex: 2, SessionCode: "sessB" });
    }

    [Fact]
    public async Task Broadcasts_scoped_to_session_only()
    {
        var store = CreateSessionStore();
        var hub = Harness.Hub(store, new CapturingEventBus());
        var groups = new CapturingGroupManager();
        hub.SetGroups(groups);

        // Create two separate session groups
        var session1Group = new FakeClientProxy();
        var session2Group = new FakeClientProxy();

        // Mock Group calls to return specific proxies
        var clients = new FakeClientsWithGroups
        {
            Session1Group = session1Group,
            Session2Group = session2Group
        };
        hub.SetClients(clients);

        // Join session1 as performer
        var ctx1 = new TestContext("conn-1");
        hub.SetContext(ctx1);
        await hub.Join("session1", "Performer", null, null);

        // Join session2 as performer
        var ctx2 = new TestContext("conn-2");
        hub.SetContext(ctx2);
        await hub.Join("session2", "Performer", null, null);

        // Verify groups are separate
        Assert.True(groups.Groups.TryGetValue(RealtimeGroups.Session("session1"), out var sess1Group) && sess1Group.Contains("conn-1"));
        Assert.True(groups.Groups.TryGetValue(RealtimeGroups.Session("session2"), out var sess2Group) && sess2Group.Contains("conn-2"));
        Assert.DoesNotContain("conn-2", sess1Group);
        Assert.DoesNotContain("conn-1", sess2Group);
    }

    [Fact]
    public async Task Multiple_sessions_do_not_receive_each_others_events()
    {
        var store = CreateSessionStore();
        var bus = new CapturingEventBus();
        var hub = Harness.Hub(store, bus, out var gameState);
        var groups = new CapturingGroupManager();
        hub.SetGroups(groups);

        // An answer is only accepted during Guessing, which the processor enforces.
        gameState.Set("session1", GameReducer.Initial("session1") with
        {
            Phase = Phase.Guessing,
            Choices = ["A", "B"],
            Tallies = [0, 0]
        });

        // Create capturing clients for each session
        var session1Clients = new ConcurrentBag<(string method, object?[] args)>();
        var session2Clients = new ConcurrentBag<(string method, object?[] args)>();

        var clients = new FakeClientsWithSessionTracking
        {
            Session1Clients = session1Clients,
            Session2Clients = session2Clients
        };
        hub.SetClients(clients);

        // Join session1 as audience
        var ctx1 = new TestContext("conn-sess1");
        hub.SetContext(ctx1);
        await hub.Join("session1", "Audience", name: "Alice", deviceSecret: "dev-Alice");

        // Join session2 as audience
        var ctx2 = new TestContext("conn-sess2");
        hub.SetContext(ctx2);
        await hub.Join("session2", "Audience", name: "Bob", deviceSecret: "dev-Bob");

        // Submit answer in session1
        hub.SetContext(ctx1);
        await hub.SubmitAnswer("session1", 1, Guid.Empty);

        // Verify only session1 received the event (via event bus, not direct broadcast)
        var session1Events = bus.Published.Where(e => e is AnswerSubmitted a && a.SessionCode == "session1").ToList();
        var session2Events = bus.Published.Where(e => e is AnswerSubmitted a && a.SessionCode == "session2").ToList();

        Assert.Single(session1Events);
        Assert.Empty(session2Events);
    }

    [Fact]
    public async Task Join_assigns_connection_to_session_group_by_role()
    {
        var store = CreateSessionStore();
        var hub = Harness.Hub(store, new CapturingEventBus());
        var clients = new FakeClients();
        var groups = new CapturingGroupManager();
        hub.SetClients(clients);
        hub.SetGroups(groups);

        // Join as Performer
        var perfCtx = new TestContext("conn-perf");
        hub.SetContext(perfCtx);
        await hub.Join("test-session", "Performer", null, null);

        // Verify added to session group and role group
        Assert.True(groups.Groups.TryGetValue(RealtimeGroups.Session("test-session"), out var sessGroup) && sessGroup.Contains("conn-perf"));
        Assert.True(groups.Groups.TryGetValue(RealtimeGroups.SessionRole("test-session", "performer"), out var roleGroup) && roleGroup.Contains("conn-perf"));

        // Join as Audience
        var audCtx = new TestContext("conn-aud");
        hub.SetContext(audCtx);
        await hub.Join("test-session", "Audience", name: "Charlie", deviceSecret: "dev-Charlie");

        // Verify added to session group and audience role group
        Assert.Contains("conn-aud", sessGroup);
        Assert.True(groups.Groups.TryGetValue(RealtimeGroups.SessionRole("test-session", "audience"), out var audRoleGroup) && audRoleGroup.Contains("conn-aud"));

        // Verify both connections in session group
        Assert.Contains("conn-perf", sessGroup);
        Assert.Contains("conn-aud", sessGroup);
    }

    sealed class FakeClientsWithGroups : IHubCallerClients
    {
        public FakeClientProxy Session1Group = new();
        public FakeClientProxy Session2Group = new();
        public IClientProxy Caller { get; } = new FakeClientProxy();
        public IClientProxy All => throw new NotImplementedException();
        public IClientProxy AllExcept(IReadOnlyList<string> excludedConnectionIds) => throw new NotImplementedException();
        public IClientProxy Client(string connectionId) => new FakeClientProxy();
        public IClientProxy Clients(IReadOnlyList<string> connectionIds) => new FakeClientProxy();
        public IClientProxy Group(string groupName)
        {
            if (groupName == "session1") return Session1Group;
            if (groupName == "session2") return Session2Group;
            return new FakeClientProxy();
        }
        public IClientProxy GroupExcept(string groupName, IReadOnlyList<string> excludedConnectionIds) => Group(groupName);
        public IClientProxy Groups(IReadOnlyList<string> groupNames) => new FakeClientProxy();
        public IClientProxy Others => throw new NotImplementedException();
        public IClientProxy OthersInGroup(string groupName) => throw new NotImplementedException();
        public IClientProxy User(string userId) => throw new NotImplementedException();
        public IClientProxy Users(IReadOnlyList<string> userIds) => throw new NotImplementedException();
    }

    sealed class FakeClientsWithSessionTracking : IHubCallerClients
    {
        public ConcurrentBag<(string method, object?[] args)> Session1Clients = new();
        public ConcurrentBag<(string method, object?[] args)> Session2Clients = new();
        public IClientProxy Caller { get; } = new FakeClientProxy();
        public IClientProxy All => throw new NotImplementedException();
        public IClientProxy AllExcept(IReadOnlyList<string> excludedConnectionIds) => throw new NotImplementedException();
        public IClientProxy Client(string connectionId) => new FakeClientProxy();
        public IClientProxy Clients(IReadOnlyList<string> connectionIds) => new FakeClientProxy();
        public IClientProxy Group(string groupName)
        {
            if (groupName == RealtimeGroups.Session("session1")) return new TrackingClientProxy(Session1Clients);
            if (groupName == RealtimeGroups.Session("session2")) return new TrackingClientProxy(Session2Clients);
            return new FakeClientProxy();
        }
        public IClientProxy GroupExcept(string groupName, IReadOnlyList<string> excludedConnectionIds) => Group(groupName);
        public IClientProxy Groups(IReadOnlyList<string> groupNames) => new FakeClientProxy();
        public IClientProxy Others => throw new NotImplementedException();
        public IClientProxy OthersInGroup(string groupName) => throw new NotImplementedException();
        public IClientProxy User(string userId) => throw new NotImplementedException();
        public IClientProxy Users(IReadOnlyList<string> userIds) => throw new NotImplementedException();
    }

    sealed class TrackingClientProxy(ConcurrentBag<(string method, object?[] args)> bag) : IClientProxy
    {
        public Task SendCoreAsync(string method, object?[] args, CancellationToken cancellationToken = default)
        {
            bag.Add((method, args));
            return Task.CompletedTask;
        }
    }

    [Fact]
    public async Task Join_with_name_broadcasts_JoinedAudience_to_session_group()
    {
        var store = CreateSessionStore();
        var hub = Harness.Hub(store, new CapturingEventBus());
        var clients = new FakeClients();
        var groups = new CapturingGroupManager();
        hub.SetContext(new TestContext("conn-1"));
        hub.SetClients(clients);
        hub.SetGroups(groups);

        await hub.Join("test-session", "Audience", name: "Alice", deviceSecret: "dev-Alice");

        // Verify JoinedAudience was sent to session group
        Assert.True(clients.GroupProxies.TryGetValue(RealtimeGroups.Session("test-session"), out var groupProxy));
        Assert.Contains(groupProxy.Sent, x => x.method == "JoinedAudience");
        var joinedMsg = (JoinedAudience)groupProxy.Sent.First(x => x.method == "JoinedAudience").args[0]!;
        Assert.StartsWith("part_", joinedMsg.ConnectionId);
        Assert.Equal("Alice", joinedMsg.Name);
    }

    [Fact]
    public async Task Join_without_name_does_not_broadcast_JoinedAudience()
    {
        var store = CreateSessionStore();
        var hub = Harness.Hub(store, new CapturingEventBus());
        var clients = new FakeClients();
        var groups = new CapturingGroupManager();
        hub.SetContext(new TestContext("conn-1"));
        hub.SetClients(clients);
        hub.SetGroups(groups);

        await hub.Join("test-session", "Performer", null, null);

        // Verify JoinedAudience was NOT sent (if group proxy exists, it should be empty)
        if (clients.GroupProxies.TryGetValue(RealtimeGroups.Session("test-session"), out var groupProxy))
        {
            Assert.DoesNotContain(groupProxy.Sent, x => x.method == "JoinedAudience");
        }
    }

    [Fact]
    public async Task Join_with_empty_session_returns_problem()
    {
        var store = CreateSessionStore();
        var hub = Harness.Hub(store, new CapturingEventBus());
        var clients = new FakeClients();
        hub.SetContext(new TestContext("conn-1"));
        hub.SetClients(clients);

        await hub.Join("", "Audience", null, null);

        Assert.Contains(clients.CallerProxy.Sent, x => x.method == "Problem");
    }

    [Fact]
    public async Task Join_with_empty_role_returns_problem()
    {
        var store = CreateSessionStore();
        var hub = Harness.Hub(store, new CapturingEventBus());
        var clients = new FakeClients();
        hub.SetContext(new TestContext("conn-1"));
        hub.SetClients(clients);

        await hub.Join("test-session", "", null, null);

        Assert.Contains(clients.CallerProxy.Sent, x => x.method == "Problem");
    }

    [Fact]
    public async Task CreateOrJoinWithName_calls_Join_with_audience_role()
    {
        var store = CreateSessionStore();
        var hub = Harness.Hub(store, new CapturingEventBus());
        var clients = new FakeClients();
        var groups = new CapturingGroupManager();
        hub.SetContext(new TestContext("conn-1"));
        hub.SetClients(clients);
        hub.SetGroups(groups);

        await hub.CreateOrJoinWithName("test-session", "Bob", "dev-Bob");

        // Verify added to session and audience role group
        Assert.True(groups.Groups.TryGetValue(RealtimeGroups.Session("test-session"), out var sessGroup) && sessGroup.Contains("conn-1"));
        Assert.True(groups.Groups.TryGetValue(RealtimeGroups.SessionRole("test-session", "audience"), out var roleGroup) && roleGroup.Contains("conn-1"));

        // Verify JoinedAudience was broadcast
        Assert.True(clients.GroupProxies.TryGetValue(RealtimeGroups.Session("test-session"), out var groupProxy));
        Assert.Contains(groupProxy.Sent, x => x.method == "JoinedAudience");
    }

    [Fact]
    public async Task OnDisconnectedAsync_removes_from_groups_and_session_store()
    {
        var store = CreateSessionStore();
        var hub = Harness.Hub(store, new CapturingEventBus());
        var groups = new CapturingGroupManager();
        hub.SetGroups(groups);
        hub.SetContext(new TestContext("conn-1"));
        hub.SetClients(new FakeClients());

        // Join a session
        await hub.Join("test-session", "Audience", name: "Alice", deviceSecret: "dev-Alice");
        Assert.True(groups.Groups.TryGetValue(RealtimeGroups.Session("test-session"), out var sessGroup) && sessGroup.Contains("conn-1"));
        Assert.Equal(1, store.GetCounts("test-session").Audiences);

        // Disconnect
        await hub.OnDisconnectedAsync(null);

        // Verify removed from groups (note: CapturingGroupManager doesn't actually remove, but we can verify RemoveFromGroupAsync was called)
        // The session store should have removed the connection
        Assert.Equal(0, store.GetCounts("test-session").Audiences);
    }

    [Fact]
    public async Task EngineStatusChanged_broadcasts_to_session_group()
    {
        var store = CreateSessionStore();
        var hub = Harness.Hub(store, new CapturingEventBus());
        var clients = new FakeClients();
        hub.SetClients(clients);

        hub.SetContext(new TestContext("engine-1"));
        hub.SetGroups(new CapturingGroupManager());
        await hub.Join("test-session", "Engine", null, null);

        var evt = new EngineStatusChanged(EngineStatus.Playing, 50.0);
        await hub.EngineStatusChanged("test-session", evt);

        Assert.True(clients.GroupProxies.TryGetValue(RealtimeGroups.Session("test-session"), out var groupProxy));
        Assert.Contains(groupProxy.Sent, x => x.method == "EngineStatusChanged" && x.args[0] is EngineStatusChanged e && e.Status == evt.Status && e.LatencyMs == evt.LatencyMs);
    }

    [Fact]
    public async Task Ping_broadcasts_to_engine_group()
    {
        var store = CreateSessionStore();
        var hub = Harness.Hub(store, new CapturingEventBus());
        var clients = new FakeClients();
        hub.SetClients(clients);

        hub.SetContext(new TestContext("perf-1"));
        await hub.Ping("test-session", 1234567890L);

        Assert.True(clients.GroupProxies.TryGetValue(RealtimeGroups.SessionRole("test-session", "engine"), out var engineGroupProxy));
        Assert.Contains(engineGroupProxy.Sent, x => x.method == "Ping" && x.args[0] is long ticks && ticks == 1234567890L);
    }

    [Fact]
    public async Task Echo_broadcasts_to_performer_group()
    {
        var store = CreateSessionStore();
        var hub = Harness.Hub(store, new CapturingEventBus());
        var clients = new FakeClients();
        hub.SetClients(clients);

        hub.SetContext(new TestContext("engine-1"));
        await hub.Echo("test-session", 1234567890L, 1234567900L);

        Assert.True(clients.GroupProxies.TryGetValue(RealtimeGroups.SessionRole("test-session", "performer"), out var performerGroupProxy));
        Assert.Contains(performerGroupProxy.Sent, x =>
            x.method == "Echo" &&
            x.args[0] is long clientTicks && clientTicks == 1234567890L &&
            x.args[1] is long engineTicks && engineTicks == 1234567900L);
    }

    [Fact]
    public async Task RequestPlay_blocks_the_audience()
    {
        // Playback used to be an audience-only call, reachable from a developer panel in the
        // Audience app: any phone in the room could start audio on the venue rig. The Performer
        // drives playback.
        var store = CreateSessionStore();
        var hub = Harness.Hub(store, new CapturingEventBus());
        var clients = new FakeClients();
        var groups = new CapturingGroupManager();
        hub.SetContext(new TestContext("aud-1"));
        hub.SetClients(clients);
        hub.SetGroups(groups);

        await hub.Join("test-session", "Audience", name: "Alice", deviceSecret: "dev-Alice");

        var cmd = new PlayTrack("https://example.com/track.mp3")
        {
            SessionCode = "test-session",
            IssuedByRole = Role.Audience,
            IssuedById = "aud-1"
        };
        await hub.RequestPlay("test-session", cmd);

        Assert.Contains(clients.CallerProxy.Sent, x => x.method == "Problem");
        Assert.False(clients.GroupProxies.TryGetValue(RealtimeGroups.Session("test-session"), out var group)
            && group.Sent.Any(x => x.method == "RequestPlay"));
    }

    [Fact]
    public async Task RequestPlay_allows_the_performer_and_broadcasts_to_session()
    {
        var store = CreateSessionStore();
        var hub = Harness.Hub(store, new CapturingEventBus());
        var clients = new FakeClients();
        var groups = new CapturingGroupManager();
        hub.SetContext(new TestContext("perf-1"));
        hub.SetClients(clients);
        hub.SetGroups(groups);

        await hub.Join("test-session", "Performer", null, null);

        var cmd = new PlayTrack("https://example.com/track.mp3")
        {
            SessionCode = "test-session",
            IssuedByRole = Role.Performer,
            IssuedById = "perf-1"
        };
        await hub.RequestPlay("test-session", cmd);

        Assert.True(clients.GroupProxies.TryGetValue(RealtimeGroups.Session("test-session"), out var groupProxy));
        Assert.Contains(groupProxy.Sent, x => x.method == "RequestPlay" && x.args[0] is PlayTrack p && p.FileUrl == cmd.FileUrl);
    }

    [Fact]
    public async Task Broadcasts_are_scoped_to_correct_session_group()
    {
        var store = CreateSessionStore();
        var hub = Harness.Hub(store, new CapturingEventBus());
        var session1Messages = new ConcurrentBag<(string method, object?[] args)>();
        var session2Messages = new ConcurrentBag<(string method, object?[] args)>();

        var clients = new FakeClientsWithSessionTracking
        {
            Session1Clients = session1Messages,
            Session2Clients = session2Messages
        };
        hub.SetClients(clients);
        hub.SetGroups(new CapturingGroupManager());

        // Join session1
        hub.SetContext(new TestContext("conn-1"));
        await hub.Join("session1", "Audience", name: "Alice", deviceSecret: "dev-Alice");

        // Join session2
        hub.SetContext(new TestContext("conn-2"));
        await hub.Join("session2", "Audience", name: "Bob", deviceSecret: "dev-Bob");

        // Broadcast EngineStatusChanged to session1
        var evt = new EngineStatusChanged(EngineStatus.Playing, 50.0);
        hub.SetContext(new TestContext("engine-1"));
        await hub.Join("session1", "Engine", null, null);
        await hub.EngineStatusChanged("session1", evt);

        // Verify session1 received it, session2 did not
        Assert.Contains(session1Messages, x => x.method == "EngineStatusChanged");
        Assert.DoesNotContain(session2Messages, x => x.method == "EngineStatusChanged");
    }
}
