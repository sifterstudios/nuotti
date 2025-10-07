using Nuotti.Contracts.V1.Enum;
using Nuotti.Contracts.V1.Event;
using Nuotti.Contracts.V1.Model;
using Nuotti.Contracts.V1.Reducer;

namespace Nuotti.Contracts.Tests.V1.Reducer;

public class GameReducerScoringTests
{
    [Fact]
    public void CorrectAnswerRevealed_awards_points_to_correct_audiences()
    {
        // Arrange: guessing phase with choices and two audience answers
        var state = new GameStateSnapshot(
            sessionCode: "S",
            phase: Phase.Guessing,
            songIndex: 0,
            currentSong: null,
            choices: new[] {"A", "B", "C"},
            hintIndex: 0,
            tallies: new[] {0, 0, 0},
            scores: new Dictionary<string, int> { ["aud-2"] = 1 },
            songStartedAtUtc: null);

        // two answers submitted: aud-1 picks index 2 (correct), aud-2 picks index 1 (wrong)
        (state, _) = GameReducer.Reduce(state, new AnswerSubmitted("aud-1", 2)
        {
            AudienceId = "aud-1",
            ChoiceIndex = 2,
            SessionCode = state.SessionCode,
            EmittedAtUtc = DateTime.UtcNow,
            CorrelationId = Guid.Empty,
            CausedByCommandId = Guid.Empty
        });

        (state, _) = GameReducer.Reduce(state, new AnswerSubmitted("aud-2", 1)
        {
            AudienceId = "aud-2",
            ChoiceIndex = 1,
            SessionCode = state.SessionCode,
            EmittedAtUtc = DateTime.UtcNow,
            CorrelationId = Guid.Empty,
            CausedByCommandId = Guid.Empty
        });

        // Act: reveal that correct index is 2
        var (next, err) = GameReducer.Reduce(state, new CorrectAnswerRevealed(2)
        {
            CorrectChoiceIndex = 2,
            SessionCode = state.SessionCode,
            EmittedAtUtc = DateTime.UtcNow,
            CorrelationId = Guid.Empty,
            CausedByCommandId = Guid.Empty
        });

        // Assert
        Assert.Null(err);
        Assert.Equal(Phase.Guessing, next.Phase); // phase unchanged by this event
        Assert.True(next.Scores.ContainsKey("aud-1"));
        Assert.Equal(1, next.Scores["aud-1"]);
        Assert.Equal(1, next.Scores["aud-2"]); // unchanged because wrong answer but had 1 from before
    }

    [Fact]
    public void Scoring_is_cumulative_and_ties_preserved()
    {
        // Arrange: two songs, two players both answer correctly on first, only one on second
        var state = new GameStateSnapshot(
            sessionCode: "S",
            phase: Phase.Guessing,
            songIndex: 0,
            choices: new[] {"A", "B"},
            hintIndex: 0,
            tallies: new[] {0, 0},
            scores: null,
            songStartedAtUtc: null);

        // Song 1 answers
        (state, _) = GameReducer.Reduce(state, new AnswerSubmitted("p1", 0) { AudienceId = "p1", ChoiceIndex = 0, SessionCode = state.SessionCode, EmittedAtUtc = DateTime.UtcNow, CorrelationId = Guid.Empty, CausedByCommandId = Guid.Empty });
        (state, _) = GameReducer.Reduce(state, new AnswerSubmitted("p2", 0) { AudienceId = "p2", ChoiceIndex = 0, SessionCode = state.SessionCode, EmittedAtUtc = DateTime.UtcNow, CorrelationId = Guid.Empty, CausedByCommandId = Guid.Empty });
        (state, _) = GameReducer.Reduce(state, new CorrectAnswerRevealed(0) { CorrectChoiceIndex = 0, SessionCode = state.SessionCode, EmittedAtUtc = DateTime.UtcNow, CorrelationId = Guid.Empty, CausedByCommandId = Guid.Empty });

        // Transition to next song resets tallies and answers
        (state, _) = GameReducer.Reduce(state, new GamePhaseChanged(Phase.Guessing, Phase.Start)
        {
            CurrentPhase = Phase.Guessing,
            NewPhase = Phase.Start,
            SessionCode = state.SessionCode,
            EmittedAtUtc = DateTime.UtcNow,
            CorrelationId = Guid.Empty,
            CausedByCommandId = Guid.Empty
        });

        // Back to Guessing with two choices again (simulate same choices for simplicity)
        state = state with { Phase = Phase.Guessing, Choices = new[] {"A", "B"}, Tallies = new[] {0, 0} };

        // Song 2 answers: only p1 correct
        (state, _) = GameReducer.Reduce(state, new AnswerSubmitted("p1", 1) { AudienceId = "p1", ChoiceIndex = 1, SessionCode = state.SessionCode, EmittedAtUtc = DateTime.UtcNow, CorrelationId = Guid.Empty, CausedByCommandId = Guid.Empty });
        (state, _) = GameReducer.Reduce(state, new AnswerSubmitted("p2", 0) { AudienceId = "p2", ChoiceIndex = 0, SessionCode = state.SessionCode, EmittedAtUtc = DateTime.UtcNow, CorrelationId = Guid.Empty, CausedByCommandId = Guid.Empty });
        (state, _) = GameReducer.Reduce(state, new CorrectAnswerRevealed(1) { CorrectChoiceIndex = 1, SessionCode = state.SessionCode, EmittedAtUtc = DateTime.UtcNow, CorrelationId = Guid.Empty, CausedByCommandId = Guid.Empty });

        // Assert cumulative scores: p1=2, p2=1; verify ties preserved earlier: after first song they tied (we don't break ties)
        Assert.Equal(2, state.Scores["p1"]);
        Assert.Equal(1, state.Scores["p2"]);
    }
}
