using Nuotti.Backend.Commands;
using Nuotti.Contracts.V1.Model;
using Nuotti.Contracts.V1.Protocol;

namespace Nuotti.Backend.Persistence;

public sealed record DurableOutboxMessage(
    string WorkspaceId,
    string SessionCode,
    SessionSequence Sequence,
    string MessageType,
    string Payload);

public sealed record DurableSessionRecord(
    string WorkspaceId,
    GameStateSnapshot Snapshot,
    SessionSequence LastSequence,
    ControlGeneration ControlGeneration);

public sealed record DurableCommit(
    CommandResult Result,
    IReadOnlyList<DurableOutboxMessage> Messages,
    bool WasDuplicate,
    bool WasStale = false);

public enum DurableCommitPrecondition { Any, SessionMustNotExist }

/// <summary>
/// Atomically owns a Session snapshot, durable command outcome, ordered Event log, and outbox.
/// Implementations must return the original outcome when CommandId already exists.
/// </summary>
public interface IDurableSessionCommitStore
{
    Task<DurableSessionRecord?> LoadAsync(string workspaceId, string sessionCode, CancellationToken cancellationToken = default);
    Task<CommandResult?> FindOutcomeAsync(string workspaceId, string sessionCode, Guid commandId, CancellationToken cancellationToken = default);
    Task<DurableCommit> CommitAsync(
        string workspaceId,
        string sessionCode,
        Guid commandId,
        SessionSequence expectedSequence,
        GameStateSnapshot snapshot,
        IReadOnlyList<object> messages,
        DurableCommitPrecondition precondition = DurableCommitPrecondition.Any,
        CancellationToken cancellationToken = default);
    Task<IReadOnlyList<DurableOutboxMessage>> ClaimPendingAsync(
        Guid owner, TimeSpan lease, int limit, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<DurableOutboxMessage>> ReadAfterAsync(
        string workspaceId,
        string sessionCode,
        SessionSequence cursor,
        CancellationToken cancellationToken = default);
    Task MarkDeliveredAsync(DurableOutboxMessage message, Guid owner, CancellationToken cancellationToken = default);
}
