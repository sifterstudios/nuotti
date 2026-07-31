using Nuotti.Backend.Commands;
using Nuotti.Contracts.V1.Model;
using Nuotti.Contracts.V1.Protocol;

namespace Nuotti.Backend.Persistence;

/// <summary>Deterministic adapter used by tests; multiple processors may share it to model restart.</summary>
public sealed class InMemoryDurableSessionCommitStore : IDurableSessionCommitStore
{
    readonly object _gate = new();
    readonly Dictionary<(string Workspace, string Session), DurableSessionRecord> _sessions = [];
    readonly Dictionary<(string Workspace, string Session, Guid Command), CommandResult> _outcomes = [];
    readonly List<(DurableOutboxMessage Message, bool Delivered, Guid? Owner, DateTimeOffset? LeaseUntil)> _outbox = [];

    public Task<DurableSessionRecord?> LoadAsync(string workspaceId, string sessionCode, CancellationToken cancellationToken = default)
    {
        lock (_gate) return Task.FromResult(_sessions.GetValueOrDefault((workspaceId, sessionCode)));
    }

    public Task<CommandResult?> FindOutcomeAsync(string workspaceId, string sessionCode, Guid commandId, CancellationToken cancellationToken = default)
    {
        lock (_gate) return Task.FromResult(_outcomes.GetValueOrDefault((workspaceId, sessionCode, commandId)));
    }

    public Task<DurableCommit> CommitAsync(
        string workspaceId,
        string sessionCode,
        Guid commandId,
        SessionSequence expectedSequence,
        GameStateSnapshot snapshot,
        IReadOnlyList<object> messages,
        DurableCommitPrecondition precondition = DurableCommitPrecondition.Any,
        CancellationToken cancellationToken = default)
    {
        lock (_gate)
        {
            if (_outcomes.TryGetValue((workspaceId, sessionCode, commandId), out var prior))
                return Task.FromResult(new DurableCommit(prior, [], WasDuplicate: true));

            var key = (workspaceId, sessionCode);
            var existing = _sessions.GetValueOrDefault(key);
            if (precondition == DurableCommitPrecondition.SessionMustNotExist && existing is not null)
                return Task.FromResult(new DurableCommit(CommandResult.Rejected(
                    NuottiProblem.Conflict("Session already exists", $"Session '{sessionCode}' has already been created.",
                        Nuotti.Contracts.V1.Enum.ReasonCode.InvalidStateTransition)), [], false));
            var sequence = existing?.LastSequence ?? SessionSequence.None;
            if (sequence != expectedSequence)
                return Task.FromResult(new DurableCommit(CommandResult.Duplicate(), [], false, WasStale: true));
            var committed = new List<DurableOutboxMessage>(messages.Count);
            foreach (var message in messages)
            {
                sequence = sequence.Next();
                var serialized = SessionMessagePublisher.SerializeDurable(message);
                var pending = new DurableOutboxMessage(workspaceId, sessionCode, sequence, serialized.Type, serialized.Payload);
                committed.Add(pending);
                _outbox.Add((pending, false, null, null));
            }

            var result = CommandResult.Applied(snapshot);
            _sessions[key] = new DurableSessionRecord(workspaceId, snapshot, sequence, ControlGeneration.Initial);
            _outcomes[(workspaceId, sessionCode, commandId)] = result;
            return Task.FromResult(new DurableCommit(result, committed, WasDuplicate: false));
        }
    }

    public Task<IReadOnlyList<DurableOutboxMessage>> ClaimPendingAsync(Guid owner, TimeSpan lease, int limit, CancellationToken cancellationToken = default)
    {
        lock (_gate)
        {
            var now = DateTimeOffset.UtcNow;
            var claimed = new List<DurableOutboxMessage>();
            for (var i = 0; i < _outbox.Count && claimed.Count < limit; i++)
            {
                var item = _outbox[i];
                if (item.Delivered || item.LeaseUntil > now) continue;
                var hasEarlier = _outbox.Any(other => !other.Delivered
                    && other.Message.WorkspaceId == item.Message.WorkspaceId
                    && other.Message.SessionCode == item.Message.SessionCode
                    && other.Message.Sequence.Value < item.Message.Sequence.Value);
                if (hasEarlier) continue;
                _outbox[i] = (item.Message, false, owner, now + lease);
                claimed.Add(item.Message);
            }
            return Task.FromResult<IReadOnlyList<DurableOutboxMessage>>(claimed);
        }
    }

    public Task<IReadOnlyList<DurableOutboxMessage>> ReadAfterAsync(
        string workspaceId,
        string sessionCode,
        SessionSequence cursor,
        CancellationToken cancellationToken = default)
    {
        lock (_gate)
            return Task.FromResult<IReadOnlyList<DurableOutboxMessage>>(_outbox
                .Where(item => item.Message.WorkspaceId == workspaceId && item.Message.SessionCode == sessionCode && item.Message.Sequence.Value > cursor.Value)
                .OrderBy(item => item.Message.Sequence.Value)
                .Select(item => item.Message)
                .ToArray());
    }

    public Task MarkDeliveredAsync(DurableOutboxMessage message, Guid owner, CancellationToken cancellationToken = default)
    {
        lock (_gate)
        {
            var index = _outbox.FindIndex(item => item.Message.WorkspaceId == message.WorkspaceId
                && item.Message.SessionCode == message.SessionCode && item.Message.Sequence == message.Sequence
                && item.Owner == owner);
            if (index >= 0) _outbox[index] = (_outbox[index].Message, true, owner, null);
        }
        return Task.CompletedTask;
    }
}
