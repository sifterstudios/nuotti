using Nuotti.Contracts.V1.Enum;
using Nuotti.Contracts.V1.Model;
using Nuotti.Contracts.V1.Protocol;
using Nuotti.Contracts.V1.Reducer;

namespace Nuotti.Contracts.V1.Recovery;

/// <summary>
/// Result of reconciling a local view with a Session recovery payload.
/// </summary>
public sealed record SessionReconcileResult(
    GameStateSnapshot Snapshot,
    ControlGeneration ControlGeneration,
    SessionSequence LastSequence,
    string ImpactSummary,
    string RecommendedAction,
    bool ControlsReady);

/// <summary>
/// Pure client reconciler: adopt the recovered snapshot, apply replay events, and surface
/// plain-language impact so controls can wait until reconciliation completes.
/// </summary>
public static class SessionReconciler
{
    public static SessionReconcileResult Apply(
        GameStateSnapshot? local,
        GameStateSnapshot recoveredSnapshot,
        ControlGeneration controlGeneration,
        SessionSequence lastSequence,
        IEnumerable<object> replayEvents)
    {
        var state = recoveredSnapshot;
        foreach (var evt in replayEvents)
        {
            var (next, error) = GameReducer.Reduce(state, evt);
            if (error is null)
                state = next;
        }

        var phaseChanged = local is not null && local.Phase != state.Phase;
        var impact = local is null
            ? $"Session restored at {state.Phase}."
            : phaseChanged
                ? $"Reconnected. Phase moved from {local.Phase} to {state.Phase}."
                : $"Reconnected. Still in {state.Phase}; catching up missed events.";

        var action = state.Phase is Phase.Guessing
            ? "Wait for reconciliation to finish before opening a new Window or locking answers."
            : "Wait for reconciliation to finish before sending commands.";

        return new SessionReconcileResult(
            state,
            controlGeneration,
            lastSequence,
            impact,
            action,
            ControlsReady: true);
    }
}
