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
    public static GameStateSnapshot Initial(string sessionCode) => new GameStateSnapshot(
        sessionCode: sessionCode,
        phase: Phase.Lobby,
        songIndex: 0,
        currentSong: null,
        choices: [],
        hintIndex: 0,
        tallies: [],
        scores: null,
        songStartedAtUtc: null);

    /// <summary>
    /// Applies an event to a state, returning a new state or an error code. Pure function.
    /// </summary>
    public static (GameStateSnapshot newState, string? error) Reduce(GameStateSnapshot state, object @event)
    {
        switch (@event)
        {
            case GamePhaseChanged phaseChanged:
            {
                if (phaseChanged.CurrentPhase != state.Phase)
                {
                    return (state, $"phase_mismatch: state={state.Phase}, eventCurrent={phaseChanged.CurrentPhase}");
                }

                // Apply phase change. If moving to Start (next song), reset tallies.
                var next = state with
                {
                    Phase = phaseChanged.NewPhase,
                    Tallies = phaseChanged.NewPhase == Phase.Start ? [] : state.Tallies,
                    Answers = phaseChanged.NewPhase == Phase.Start ? System.Collections.Frozen.FrozenDictionary<string, int>.Empty : state.Answers
                };
                return (next, null);
            }
            case AnswerSubmitted answer:
            {
                // Only aggregate answers during Guessing.
                if (state.Phase != Phase.Guessing)
                {
                    return (state, null);
                }

                var idx = answer.ChoiceIndex;
                // Bounds check against Choices; if out of range, ignore.
                if (idx < 0 || idx >= state.Choices.Count)
                {
                    return (state, null);
                }

                // Ensure Tallies has at least Choices length; pad with zeros if necessary.
                var needed = state.Choices.Count;
                var tallies = state.Tallies.ToArray();
                if (tallies.Length < needed)
                {
                    Array.Resize(ref tallies, needed);
                }

                // Increment selected choice tally.
                checked { tallies[idx] += 1; }

                // Upsert per-audience last answer
                var answers = state.Answers.Count == 0
                    ? new Dictionary<string, int>()
                    : new Dictionary<string, int>(state.Answers);
                answers[answer.AudienceId] = idx;

                var updated = state with
                {
                    Tallies = tallies,
                    Answers = answers
                };
                return (updated, null);
            }
            case CorrectAnswerRevealed revealed:
            {
                // On reveal, award +1 per audience whose last submitted answer matches the correct index.
                var correctIdx = revealed.CorrectChoiceIndex;
                if (correctIdx < 0 || correctIdx >= state.Choices.Count)
                {
                    // ignore if index is invalid for current choices
                    return (state, null);
                }

                // Start from existing scores
                var scores = state.Scores.Count == 0
                    ? new Dictionary<string, int>()
                    : new Dictionary<string, int>(state.Scores);

                foreach (var kvp in state.Answers)
                {
                    if (kvp.Value == correctIdx)
                    {
                        // award +1
                        if (scores.TryGetValue(kvp.Key, out var current))
                        {
                            checked { scores[kvp.Key] = current + 1; }
                        }
                        else
                        {
                            scores[kvp.Key] = 1;
                        }
                    }
                }

                var next = state with { Scores = scores };
                return (next, null);
            }
            default:
                // Unknown events are no-ops in this reducer
                return (state, null);
        }
    }
}