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

                    // Apply phase change. If moving to Start (next song), reset tallies and round scoring.
                    var next = state with
                    {
                        Phase = phaseChanged.NewPhase,
                        Tallies = phaseChanged.NewPhase == Phase.Start ? [] : state.Tallies,
                        Answers = phaseChanged.NewPhase == Phase.Start
                            ? System.Collections.Frozen.FrozenDictionary<string, int>.Empty
                            : state.Answers,
                        AnswerReceivedAtUtc = phaseChanged.NewPhase == Phase.Start
                            ? System.Collections.Frozen.FrozenDictionary<string, DateTime>.Empty
                            : state.AnswerReceivedAtUtc,
                        LockedAnswers = phaseChanged.NewPhase == Phase.Start
                            ? System.Collections.Frozen.FrozenDictionary<string, int>.Empty
                            : phaseChanged.NewPhase == Phase.Lock
                                ? FreezeAnswers(state)
                                : state.LockedAnswers,
                        LockedAnswerReceivedAtUtc = phaseChanged.NewPhase == Phase.Start
                            ? System.Collections.Frozen.FrozenDictionary<string, DateTime>.Empty
                            : phaseChanged.NewPhase == Phase.Lock
                                ? FreezeReceipts(state)
                                : state.LockedAnswerReceivedAtUtc,
                        ScoresFinalized = phaseChanged.NewPhase == Phase.Start ? false : state.ScoresFinalized,
                        GuessingWindowDeadlineUtc = phaseChanged.NewPhase is Phase.Guessing
                            ? state.GuessingWindowDeadlineUtc
                            : null
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

                    // Upsert per-audience last answer. A revise moves the tally to the final choice.
                    var answers = state.Answers.Count == 0
                        ? new Dictionary<string, int>()
                        : new Dictionary<string, int>(state.Answers);
                    if (answers.TryGetValue(answer.AudienceId, out var previousIdx))
                    {
                        if (previousIdx == idx)
                        {
                            return (state, null);
                        }

                        if (previousIdx >= 0 && previousIdx < tallies.Length && tallies[previousIdx] > 0)
                        {
                            tallies[previousIdx] -= 1;
                        }
                    }

                    checked { tallies[idx] += 1; }
                    answers[answer.AudienceId] = idx;

                    var receipts = state.AnswerReceivedAtUtc.Count == 0
                        ? new Dictionary<string, DateTime>()
                        : new Dictionary<string, DateTime>(state.AnswerReceivedAtUtc);
                    receipts[answer.AudienceId] = answer.EmittedAtUtc;

                    var updated = state with
                    {
                        Tallies = tallies,
                        Answers = answers,
                        AnswerReceivedAtUtc = receipts
                    };
                    return (updated, null);
                }
            case CorrectAnswerRevealed revealed:
                {
                    if (state.ScoresFinalized)
                        return (state, null);

                    var correctIdx = revealed.CorrectChoiceIndex;
                    if (correctIdx < 0 || correctIdx >= state.Choices.Count)
                    {
                        return (state, null);
                    }

                    var scores = state.Scores.Count == 0
                        ? new Dictionary<string, int>()
                        : new Dictionary<string, int>(state.Scores);
                    var policy = state.ScoringPolicy;
                    // Prefer Lock-held answers; fall back to live Answers when Reveal skipped Lock.
                    var heldAnswers = state.LockedAnswers.Count > 0 ? state.LockedAnswers : state.Answers;
                    var heldReceipts = state.LockedAnswerReceivedAtUtc.Count > 0
                        ? state.LockedAnswerReceivedAtUtc
                        : state.AnswerReceivedAtUtc;
                    var windowOpened = state.GuessingWindowOpenedAtUtc ?? revealed.EmittedAtUtc;

                    foreach (var kvp in heldAnswers)
                    {
                        if (kvp.Value != correctIdx) continue;

                        int award;
                        if (policy is null)
                        {
                            award = 1;
                        }
                        else
                        {
                            heldReceipts.TryGetValue(kvp.Key, out var receivedAt);
                            if (receivedAt == default) receivedAt = revealed.EmittedAtUtc;
                            award = ScoringCalculator.PointsForCorrect(policy, windowOpened, receivedAt);
                        }

                        if (scores.TryGetValue(kvp.Key, out var current))
                            checked { scores[kvp.Key] = current + award; }
                        else
                            scores[kvp.Key] = award;
                    }

                    return (state with
                    {
                        Scores = scores,
                        ScoresFinalized = true
                    }, null);
                }
            case HintGiven hint:
                {
                    // The command guard establishes that hints are only given while Guessing;
                    // the reducer trusts the event and records the index it carries.
                    return (state with { HintIndex = hint.HintIndex }, null);
                }
            case CatalogUpdated catalogUpdated:
                {
                    return (state with { Catalog = catalogUpdated.Catalog }, null);
                }
            case CurrentSongSet songSet:
                {
                    return (state with
                    {
                        CurrentSong = songSet.Song,
                        SongIndex = songSet.SongIndex,
                        HintIndex = 0
                    }, null);
                }
            case QuestionOffered offered:
                {
                    // Re-offering the same choices is a no-op. QuestionPushed skips idempotency by
                    // design (docs/adr/0002), so a client re-sending after a dropped connection
                    // arrives here twice — and zeroing tallies unconditionally would silently wipe
                    // every vote already cast. Resetting the round is GamePhaseChanged -> Start's job,
                    // not a duplicate relay's.
                    //
                    // This equality check is on Choices content alone: the snapshot carries no
                    // question identity. A genuinely different question that happens to reuse the
                    // same option list, with no intervening Start, is indistinguishable from a
                    // duplicate relay and is treated as one — a known blind spot, not exact duplicate
                    // detection.
                    if (state.Choices.SequenceEqual(offered.Choices))
                    {
                        return (state, null);
                    }

                    // Genuinely different choices replace the previous set; tallies are re-sized and
                    // zeroed to match, mirroring a fresh question. Answers are cleared too, exactly
                    // like GamePhaseChanged -> Start above: a different question invalidates answers
                    // to the previous one, otherwise a later reveal could award a point for a choice
                    // index the audience member cast against a question they never saw. Phase and
                    // SongIndex are untouched here — those move via GamePhaseChanged.
                    return (state with
                    {
                        Choices = offered.Choices,
                        Tallies = new int[offered.Choices.Count],
                        Answers = System.Collections.Frozen.FrozenDictionary<string, int>.Empty
                    }, null);
                }
            default:
                // Unknown events are no-ops in this reducer
                return (state, null);
        }
    }

    static IReadOnlyDictionary<string, int> FreezeAnswers(GameStateSnapshot state)
    {
        var locked = state.LockedAnswers.Count == 0
            ? new Dictionary<string, int>()
            : new Dictionary<string, int>(state.LockedAnswers);
        foreach (var kvp in state.Answers)
            locked[kvp.Key] = kvp.Value;
        return locked;
    }

    static IReadOnlyDictionary<string, DateTime> FreezeReceipts(GameStateSnapshot state)
    {
        var locked = state.LockedAnswerReceivedAtUtc.Count == 0
            ? new Dictionary<string, DateTime>()
            : new Dictionary<string, DateTime>(state.LockedAnswerReceivedAtUtc);
        foreach (var kvp in state.Answers)
        {
            if (!state.AnswerReceivedAtUtc.TryGetValue(kvp.Key, out var received))
                continue;
            // Unchanged choice keeps the earliest receipt so later Windows do not penalize.
            if (state.LockedAnswers.TryGetValue(kvp.Key, out var priorChoice) && priorChoice == kvp.Value
                && locked.TryGetValue(kvp.Key, out var priorReceived) && priorReceived < received)
            {
                continue;
            }

            locked[kvp.Key] = received;
        }

        return locked;
    }
}
