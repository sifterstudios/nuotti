using Nuotti.Contracts.V1.Enum;
using Nuotti.Contracts.V1.Event;
using Nuotti.Contracts.V1.Model;
using Nuotti.Contracts.V1.Reducer;

namespace Nuotti.Contracts.Tests.V1.Reducer;

public class ProgressiveWindowScoringTests
{
    [Fact]
    public void CorrectAnswerRevealed_is_idempotent_once_finalized()
    {
        var state = GuessingState(ScoringPolicy.Standard);
        (state, _) = GameReducer.Reduce(state, Answer("p1", 0, state));
        (state, _) = GameReducer.Reduce(state, Reveal(0, state));
        Assert.True(state.ScoresFinalized);
        Assert.Equal(1500, state.Scores["p1"]);

        var (again, err) = GameReducer.Reduce(state, Reveal(0, state));
        Assert.Null(err);
        Assert.Equal(1500, again.Scores["p1"]);
    }

    [Fact]
    public void Preserved_earlier_correctness_is_not_penalized_by_later_receipt()
    {
        var opened = DateTime.UtcNow.AddSeconds(-30);
        var early = opened; // t=0 → ceiling
        var state = GuessingState(ScoringPolicy.Standard) with { GuessingWindowOpenedAtUtc = opened };
        (state, _) = GameReducer.Reduce(state, Answer("p1", 0, state, early));
        (state, _) = GameReducer.Reduce(state, new GamePhaseChanged(Phase.Guessing, Phase.Lock)
        {
            CurrentPhase = Phase.Guessing,
            NewPhase = Phase.Lock,
            SessionCode = state.SessionCode,
            EmittedAtUtc = DateTime.UtcNow,
            CorrelationId = Guid.Empty,
            CausedByCommandId = Guid.Empty
        });
        // Later Window: same correct choice with a late receipt — Lock keeps the early timestamp.
        (state, _) = GameReducer.Reduce(state, new GamePhaseChanged(Phase.Lock, Phase.Guessing)
        {
            CurrentPhase = Phase.Lock,
            NewPhase = Phase.Guessing,
            SessionCode = state.SessionCode,
            EmittedAtUtc = DateTime.UtcNow,
            CorrelationId = Guid.Empty,
            CausedByCommandId = Guid.Empty
        });
        state = state with
        {
            GuessingWindowOpenedAtUtc = opened,
            Answers = System.Collections.Frozen.FrozenDictionary<string, int>.Empty,
            AnswerReceivedAtUtc = System.Collections.Frozen.FrozenDictionary<string, DateTime>.Empty
        };
        (state, _) = GameReducer.Reduce(state, Answer("p1", 0, state, opened.AddSeconds(25)));
        (state, _) = GameReducer.Reduce(state, new GamePhaseChanged(Phase.Guessing, Phase.Lock)
        {
            CurrentPhase = Phase.Guessing,
            NewPhase = Phase.Lock,
            SessionCode = state.SessionCode,
            EmittedAtUtc = DateTime.UtcNow,
            CorrelationId = Guid.Empty,
            CausedByCommandId = Guid.Empty
        });
        (state, _) = GameReducer.Reduce(state, Reveal(0, state));
        Assert.Equal(1500, state.Scores["p1"]);
    }

    [Fact]
    public void Answers_outside_Guessing_do_not_count_after_Lock()
    {
        var state = GuessingState(null);
        (state, _) = GameReducer.Reduce(state, Answer("p1", 0, state));
        (state, _) = GameReducer.Reduce(state, new GamePhaseChanged(Phase.Guessing, Phase.Lock)
        {
            CurrentPhase = Phase.Guessing,
            NewPhase = Phase.Lock,
            SessionCode = state.SessionCode,
            EmittedAtUtc = DateTime.UtcNow,
            CorrelationId = Guid.Empty,
            CausedByCommandId = Guid.Empty
        });
        var lockedChoice = state.Answers["p1"];
        (state, _) = GameReducer.Reduce(state, Answer("p1", 1, state));
        Assert.Equal(lockedChoice, state.Answers["p1"]);
    }

    static GameStateSnapshot GuessingState(ScoringPolicy? policy) =>
        new GameStateSnapshot(
            sessionCode: "S",
            phase: Phase.Guessing,
            songIndex: 0,
            choices: ["A", "B"],
            tallies: [0, 0],
            scores: null) with
        {
            ScoringPolicy = policy,
            GuessingWindowSeconds = 30,
            GuessingWindowOpenedAtUtc = DateTime.UtcNow
        };

    static AnswerSubmitted Answer(string id, int choice, GameStateSnapshot state, DateTime? at = null) =>
        new(id, choice)
        {
            AudienceId = id,
            ChoiceIndex = choice,
            SessionCode = state.SessionCode,
            EmittedAtUtc = at ?? DateTime.UtcNow,
            CorrelationId = Guid.Empty,
            CausedByCommandId = Guid.Empty
        };

    static CorrectAnswerRevealed Reveal(int correct, GameStateSnapshot state) => new(correct)
    {
        CorrectChoiceIndex = correct,
        SessionCode = state.SessionCode,
        EmittedAtUtc = DateTime.UtcNow,
        CorrelationId = Guid.Empty,
        CausedByCommandId = Guid.Empty
    };
}
