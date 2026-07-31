using Microsoft.Extensions.Logging.Abstractions;
using Nuotti.Backend.Commands;
using Nuotti.Backend.Idempotency;
using Nuotti.Backend.Models;
using Nuotti.Backend.Persistence;
using Nuotti.Backend.Sessions;
using Nuotti.Backend.Tests.TestSupport;
using Nuotti.Contracts.V1.Enum;
using Nuotti.Contracts.V1.Eventing;
using Nuotti.Contracts.V1.Message.Phase;
using Nuotti.Contracts.V1.Protocol;
using Microsoft.Extensions.Options;

namespace Nuotti.Backend.Tests;

public sealed class DurableSessionCommandTests
{
    const string Session = "DURABLE1";
    const string Workspace = "workspace-1";

    static StartGame Command(Guid id) => new()
    {
        CommandId = id,
        SessionCode = Session,
        IssuedByRole = Role.Performer,
        IssuedById = "performer-1"
    };

    static SessionCommandProcessor Processor(
        IDurableSessionCommitStore durable,
        IEventBus bus,
        IGameStateStore? memory = null)
        => new(
            memory ?? new InMemoryGameStateStore(),
            new InMemoryIdempotencyStore(Options.Create(new NuottiOptions())),
            bus,
            NullLogger<SessionCommandProcessor>.Instance,
            durable: durable);

    [Fact]
    public async Task Retry_after_processor_restart_returns_the_durable_prior_outcome()
    {
        var durable = new InMemoryDurableSessionCommitStore();
        var id = Guid.NewGuid();
        var firstBus = new CapturingEventBus();

        var first = await Processor(durable, firstBus).ApplyAsync(
            Session, Actor.Verified(Role.Performer, "performer-1"), Command(id));
        var retryBus = new CapturingEventBus();
        var retry = await Processor(durable, retryBus).ApplyAsync(
            Session, Actor.Verified(Role.Performer, "performer-1"), Command(id));

        Assert.Equal(Outcome.Applied, first.Outcome);
        Assert.Equal(first, retry);
        Assert.Empty(retryBus.Published);
        Assert.Equal(Phase.Start, (await durable.LoadAsync("legacy", Session))!.Snapshot.Phase);
    }

    [Fact]
    public async Task Dispatcher_publishes_an_event_left_pending_after_commit()
    {
        var durable = new InMemoryDurableSessionCommitStore();
        var processor = Processor(durable, new ThrowingEventBus());

        var result = await processor.ApplyAsync(
            Session, Actor.Verified(Role.Performer, "performer-1"), Command(Guid.NewGuid()));

        Assert.Equal(Outcome.Applied, result.Outcome);
        var recoveredBus = new CapturingEventBus();
        var dispatcher = new DurableOutboxDispatcher(
            durable, recoveredBus, NullLogger<DurableOutboxDispatcher>.Instance);
        var delivered = await dispatcher.DispatchPendingAsync();
        delivered += await dispatcher.DispatchPendingAsync();

        Assert.Equal(2, delivered);
        Assert.Equal(2, recoveredBus.Published.Count);
    }

    [Fact]
    public async Task Restart_can_load_snapshot_and_replay_without_replica_memory()
    {
        var durable = new InMemoryDurableSessionCommitStore();
        await Processor(durable, new CapturingEventBus()).ApplyAsync(
            Session, Actor.Verified(Role.Performer, "performer-1"), Command(Guid.NewGuid()));

        var snapshot = await durable.LoadAsync("legacy", Session);
        var replay = await durable.ReadAfterAsync("legacy", Session, SessionSequence.None);

        Assert.NotNull(snapshot);
        Assert.Equal(Phase.Start, snapshot!.Snapshot.Phase);
        Assert.Equal(snapshot.LastSequence.Value, replay[^1].Sequence.Value);
        Assert.All(replay.Zip(replay.Skip(1)), pair =>
            Assert.True(pair.First.Sequence.Value < pair.Second.Sequence.Value));
    }

    [Fact]
    public async Task Workspace_scope_is_part_of_every_durable_key()
    {
        var durable = new InMemoryDurableSessionCommitStore();
        var commandId = Guid.NewGuid();
        await Processor(durable, new CapturingEventBus()).ApplyAsync(
            Session, Actor.Verified(Role.Performer, "performer-1"), Command(commandId), workspaceId: Workspace);

        Assert.NotNull(await durable.LoadAsync(Workspace, Session));
        Assert.Null(await durable.LoadAsync("another-workspace", Session));
        Assert.NotNull(await durable.FindOutcomeAsync(Workspace, Session, commandId));
        Assert.Null(await durable.FindOutcomeAsync("another-workspace", Session, commandId));
    }

    [Fact]
    public async Task Concurrent_create_with_different_ids_cannot_overwrite_the_first_session()
    {
        var durable = new InMemoryDurableSessionCommitStore();
        var first = new CreateSession(Session) { SessionCode = Session, IssuedByRole = Role.Performer, IssuedById = "p1" };
        var second = new CreateSession(Session) { SessionCode = Session, IssuedByRole = Role.Performer, IssuedById = "p2" };

        var results = await Task.WhenAll(
            Processor(durable, new CapturingEventBus()).ApplyAsync(Session, Actor.Verified(Role.Performer, "p1"), first, workspaceId: Workspace),
            Processor(durable, new CapturingEventBus()).ApplyAsync(Session, Actor.Verified(Role.Performer, "p2"), second, workspaceId: Workspace));

        Assert.Single(results, result => result.Outcome == Outcome.Applied);
        Assert.Single(results, result => result.Outcome == Outcome.Rejected);
        Assert.Equal(Phase.Lobby, (await durable.LoadAsync(Workspace, Session))!.Snapshot.Phase);
    }

    [Fact]
    public async Task Stale_commit_cannot_overwrite_a_newer_snapshot()
    {
        var durable = new InMemoryDurableSessionCommitStore();
        await Processor(durable, new CapturingEventBus()).ApplyAsync(
            Session, Actor.Verified(Role.Performer, "performer-1"), Command(Guid.NewGuid()), workspaceId: Workspace);

        var stale = await durable.CommitAsync(
            Workspace, Session, Guid.NewGuid(), SessionSequence.None,
            new Nuotti.Contracts.V1.Model.GameStateSnapshot(Session, Phase.Finished, 0), []);

        Assert.True(stale.WasStale);
        Assert.Equal(Phase.Start, (await durable.LoadAsync(Workspace, Session))!.Snapshot.Phase);
    }

    [Fact]
    public async Task Claims_are_exclusive_and_preserve_per_session_order()
    {
        var durable = new InMemoryDurableSessionCommitStore();
        await Processor(durable, new CapturingEventBus()).ApplyAsync(
            Session, Actor.Verified(Role.Performer, "performer-1"), Command(Guid.NewGuid()), workspaceId: Workspace);
        var owner1 = Guid.NewGuid();
        var owner2 = Guid.NewGuid();

        var first = Assert.Single(await durable.ClaimPendingAsync(owner1, TimeSpan.FromMinutes(1), 10));
        Assert.Empty(await durable.ClaimPendingAsync(owner2, TimeSpan.FromMinutes(1), 10));
        await durable.MarkDeliveredAsync(first, owner1);
        var second = Assert.Single(await durable.ClaimPendingAsync(owner2, TimeSpan.FromMinutes(1), 10));

        Assert.True(first.Sequence.Value < second.Sequence.Value);
    }

    [Fact]
    public async Task Repeated_stale_commits_return_busy_rejection_instead_of_throwing()
    {
        var result = await Processor(new AlwaysStaleStore(), new CapturingEventBus()).ApplyAsync(
            Session, Actor.Verified(Role.Performer, "performer-1"), Command(Guid.NewGuid()), workspaceId: Workspace);

        Assert.Equal(Outcome.Rejected, result.Outcome);
        Assert.Equal(409, result.Problem!.Status);
    }

    sealed class ThrowingEventBus : IEventBus
    {
        public IDisposable Subscribe<TEvent>(Func<TEvent, CancellationToken, Task> handler) => new Noop();
        public Task PublishAsync<TEvent>(TEvent evt, CancellationToken cancellationToken = default)
            => throw new InvalidOperationException("simulated crash after commit");
        sealed class Noop : IDisposable { public void Dispose() { } }
    }

    sealed class AlwaysStaleStore : IDurableSessionCommitStore
    {
        public Task<DurableSessionRecord?> LoadAsync(string workspaceId, string sessionCode, CancellationToken cancellationToken = default)
            => Task.FromResult<DurableSessionRecord?>(null);
        public Task<CommandResult?> FindOutcomeAsync(string workspaceId, string sessionCode, Guid commandId, CancellationToken cancellationToken = default)
            => Task.FromResult<CommandResult?>(null);
        public Task<DurableCommit> CommitAsync(string workspaceId, string sessionCode, Guid commandId,
            SessionSequence expectedSequence, Nuotti.Contracts.V1.Model.GameStateSnapshot snapshot,
            IReadOnlyList<object> messages, DurableCommitPrecondition precondition = DurableCommitPrecondition.Any,
            CancellationToken cancellationToken = default)
            => Task.FromResult(new DurableCommit(CommandResult.Duplicate(), [], false, WasStale: true));
        public Task<IReadOnlyList<DurableOutboxMessage>> ClaimPendingAsync(Guid owner, TimeSpan lease, int limit, CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<DurableOutboxMessage>>([]);
        public Task<IReadOnlyList<DurableOutboxMessage>> ReadAfterAsync(string workspaceId, string sessionCode, SessionSequence cursor, CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<DurableOutboxMessage>>([]);
        public Task MarkDeliveredAsync(DurableOutboxMessage message, Guid owner, CancellationToken cancellationToken = default)
            => Task.CompletedTask;
    }
}
