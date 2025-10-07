using Nuotti.Contracts.V1.Enum;
using Nuotti.Contracts.V1.Event;
using Nuotti.Contracts.V1.Model;

namespace Nuotti.Contracts.V1.Reducer;

/// <summary>
/// Pure reducer for applying events to a GameStateSnapshot.
/// Transition legality is validated at command level; reducer trusts events except for phase mismatch.
/// </summary>
public static class GameReducer
{
    /// <summary>
    /// Initial state factory for a session. Starts in Lobby with empty collections.
    /// </summary>
    public static GameStateSnapshot Initial(string sessionCode) => new(
        sessionCode: sessionCode,
        phase: Phase.Lobby,
        songIndex: 0,
        currentSong: null,
        choices: [],
        hintIndex: 0,
        tallies: [],
        scores: null,
        songStartedAtUtc: null
    );

    /// <summary>
    /// Applies an event to a state, returning a new state or an error code. Pure function.
    /// </summary>
    public static (GameStateSnapshot newState, string? error) Reduce(GameStateSnapshot state, object @event)
    {
        switch (@event)
        {
            case GamePhaseChanged phaseChanged:
            {
                // Guard that event matches the current state
                if (phaseChanged.CurrentPhase != state.Phase)
                {
                    return (state, $"phase_mismatch: state={state.Phase}, eventCurrent={phaseChanged.CurrentPhase}");
                }

                var next = state with { Phase = phaseChanged.NewPhase };
                return (next, null);
            }
            default:
                // Unknown events are no-ops in this reducer
                return (state, null);
        }
    }
}
