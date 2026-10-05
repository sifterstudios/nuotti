using Nuotti.Backend.Persistence;
using Nuotti.Contracts.V1.Model;
using Nuotti.Contracts.V1.Protocol;

namespace Nuotti.Backend.Commands;

/// <summary>
/// Pure plan for how a reduced Session mutation is committed and published.
/// Encodes durable vs live vs ADR 0002 at-least-once relay side-commit in one place.
/// </summary>
public sealed record SessionCommitPlan(
    GameStateSnapshot HotSnapshot,
    bool StateChanged,
    IReadOnlyList<object> LivePublications,
    IReadOnlyList<object> DurablePublications,
    Guid CommandIdForDurableCommit,
    SessionSequence ExpectedSequence,
    /// <summary>Primary durable+idempotent path: Commit → hot set → outbox → live relays only.</summary>
    bool UsePrimaryDurableCommit,
    /// <summary>
    /// After live publish, commit durable-only messages under a fresh CommandId so relays stay
    /// at-least-once on the wire (docs/adr/0002) while the Workspace snapshot still advances.
    /// </summary>
    bool SideCommitDurableWithFreshCommandId)
{
    /// <summary>
    /// Build the plan after effects + reduce. Does not I/O — callers execute against stores.
    /// </summary>
    public static SessionCommitPlan Build(
        bool checkIdempotency,
        IReadOnlyList<object> publications,
        GameStateSnapshot next,
        bool stateChanged,
        SessionSequence expectedSequence,
        bool durableConfigured,
        string workspaceId,
        Guid commandId)
    {
        if (durableConfigured && checkIdempotency)
        {
            return new SessionCommitPlan(
                HotSnapshot: next,
                StateChanged: stateChanged,
                LivePublications: publications.Where(p => !SessionMessagePublisher.IsDurable(p)).ToList(),
                DurablePublications: publications.Where(SessionMessagePublisher.IsDurable).ToList(),
                CommandIdForDurableCommit: commandId,
                ExpectedSequence: expectedSequence,
                UsePrimaryDurableCommit: true,
                SideCommitDurableWithFreshCommandId: false);
        }

        var durableOnly = publications.Where(SessionMessagePublisher.IsDurable).ToList();
        var sideCommit = durableConfigured
            && stateChanged
            && workspaceId != "legacy"
            && durableOnly.Count > 0;

        return new SessionCommitPlan(
            HotSnapshot: next,
            StateChanged: stateChanged,
            LivePublications: publications,
            DurablePublications: sideCommit ? durableOnly : Array.Empty<object>(),
            // Fresh id only used when SideCommitDurableWithFreshCommandId is true.
            CommandIdForDurableCommit: Guid.NewGuid(),
            ExpectedSequence: expectedSequence,
            UsePrimaryDurableCommit: false,
            SideCommitDurableWithFreshCommandId: sideCommit);
    }
}
