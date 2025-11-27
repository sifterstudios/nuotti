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
            choices: ["A", "B", "C"],
            hintIndex: 0,
            tallies: [0, 0, 0],
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

        // Act: reveal that the correct index is 2
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
        // Arrange: two songs, two players both answer correctly on the first, only one on the second
        var state = new GameStateSnapshot(
            sessionCode: "S",
            phase: Phase.Guessing,
            songIndex: 0,
            choices: ["A", "B"],
            hintIndex: 0,
            tallies: [0, 0],
            scores: null,
            songStartedAtUtc: null);

        // Song 1 answers
        (state, _) = GameReducer.Reduce(state, new AnswerSubmitted("p1", 0) { AudienceId = "p1", ChoiceIndex = 0, SessionCode = state.SessionCode, EmittedAtUtc = DateTime.UtcNow, CorrelationId = Guid.Empty, CausedByCommandId = Guid.Empty });
        (state, _) = GameReducer.Reduce(state, new AnswerSubmitted("p2", 0) { AudienceId = "p2", ChoiceIndex = 0, SessionCode = state.SessionCode, EmittedAtUtc = DateTime.UtcNow, CorrelationId = Guid.Empty, CausedByCommandId = Guid.Empty });
        (state, _) = GameReducer.Reduce(state, new CorrectAnswerRevealed(0) { CorrectChoiceIndex = 0, SessionCode = state.SessionCode, EmittedAtUtc = DateTime.UtcNow, CorrelationId = Guid.Empty, CausedByCommandId = Guid.Empty });

        // Transition to the next song resets tallies and answers
        (state, _) = GameReducer.Reduce(state, new GamePhaseChanged(Phase.Guessing, Phase.Start)
        {
            CurrentPhase = Phase.Guessing,
            NewPhase = Phase.Start,
            SessionCode = state.SessionCode,
            EmittedAtUtc = DateTime.UtcNow,
            CorrelationId = Guid.Empty,
            CausedByCommandId = Guid.Empty
        });

        // Back to Guessing with two choices again (simulate the same choices for simplicity)
        state = state with { Phase = Phase.Guessing, Choices = ["A", "B"], Tallies = [0, 0]
        };

        // Song 2 answers: only p1 correct
        (state, _) = GameReducer.Reduce(state, new AnswerSubmitted("p1", 1) { AudienceId = "p1", ChoiceIndex = 1, SessionCode = state.SessionCode, EmittedAtUtc = DateTime.UtcNow, CorrelationId = Guid.Empty, CausedByCommandId = Guid.Empty });
        (state, _) = GameReducer.Reduce(state, new AnswerSubmitted("p2", 0) { AudienceId = "p2", ChoiceIndex = 0, SessionCode = state.SessionCode, EmittedAtUtc = DateTime.UtcNow, CorrelationId = Guid.Empty, CausedByCommandId = Guid.Empty });
        (state, _) = GameReducer.Reduce(state, new CorrectAnswerRevealed(1) { CorrectChoiceIndex = 1, SessionCode = state.SessionCode, EmittedAtUtc = DateTime.UtcNow, CorrelationId = Guid.Empty, CausedByCommandId = Guid.Empty });

        // Assert cumulative scores: p1=2, p2=1; verify ties preserved earlier: after the first song they tied (we don't break ties)
        Assert.Equal(2, state.Scores["p1"]);
        Assert.Equal(1, state.Scores["p2"]);
    }

    [Fact]
    public void CorrectAnswerRevealed_awards_plus_one_on_correct_no_points_otherwise()
    {
        var state = new GameStateSnapshot(
            sessionCode: "TEST",
            phase: Phase.Guessing,
            songIndex: 1,
            currentSong: null,
            choices: ["A", "B", "C"],
            hintIndex: 0,
            tallies: [0, 0, 0],
            scores: null,
            songStartedAtUtc: null);

        // Submit answers: player1 correct (index 1), player2 wrong (index 0), player3 correct (index 1)
        (state, _) = GameReducer.Reduce(state, new AnswerSubmitted("player1", 1)
        {
            AudienceId = "player1",
            ChoiceIndex = 1,
            SessionCode = state.SessionCode,
            EmittedAtUtc = DateTime.UtcNow,
            CorrelationId = Guid.Empty,
            CausedByCommandId = Guid.Empty
        });

        (state, _) = GameReducer.Reduce(state, new AnswerSubmitted("player2", 0)
        {
            AudienceId = "player2",
            ChoiceIndex = 0,
            SessionCode = state.SessionCode,
            EmittedAtUtc = DateTime.UtcNow,
            CorrelationId = Guid.Empty,
            CausedByCommandId = Guid.Empty
        });

        (state, _) = GameReducer.Reduce(state, new AnswerSubmitted("player3", 1)
        {
            AudienceId = "player3",
            ChoiceIndex = 1,
            SessionCode = state.SessionCode,
            EmittedAtUtc = DateTime.UtcNow,
            CorrelationId = Guid.Empty,
            CausedByCommandId = Guid.Empty
        });

        // Reveal correct answer is index 1
        (state, _) = GameReducer.Reduce(state, new CorrectAnswerRevealed(1)
        {
            CorrectChoiceIndex = 1,
            SessionCode = state.SessionCode,
            EmittedAtUtc = DateTime.UtcNow,
            CorrelationId = Guid.Empty,
            CausedByCommandId = Guid.Empty
        });

        // Assert: player1 and player3 get +1, player2 gets 0
        Assert.Equal(1, state.Scores["player1"]);
        Assert.Equal(1, state.Scores["player3"]);
        Assert.False(state.Scores.ContainsKey("player2")); // No points for wrong answer
    }

    [Fact]
    public void Stable_tie_ordering_uses_deterministic_key()
    {
        // Create state with multiple players having the same score
        var scores = new Dictionary<string, int>
        {
            ["zoe"] = 5,
            ["alice"] = 7,
            ["charlie"] = 7,
            ["bob"] = 7,
            ["dave"] = 3
        };

        var state = new GameStateSnapshot(
            sessionCode: "TEST",
            phase: Phase.Reveal,
            songIndex: 1,
            currentSong: null,
            choices: [],
            hintIndex: 0,
            tallies: [],
            scores: scores,
            songStartedAtUtc: null);

        // Order by score desc, then by key (player id) ascending for deterministic ties
        var ordered = state.Scores
            .OrderByDescending(kvp => kvp.Value)
            .ThenBy(kvp => kvp.Key, StringComparer.Ordinal)
            .ToList();

        // Verify ordering: 7 points (alice, bob, charlie), then 5 (zoe), then 3 (dave)
        // Ties resolved alphabetically: alice < bob < charlie
        Assert.Equal("alice", ordered[0].Key);
        Assert.Equal(7, ordered[0].Value);
        Assert.Equal("bob", ordered[1].Key);
        Assert.Equal(7, ordered[1].Value);
        Assert.Equal("charlie", ordered[2].Key);
        Assert.Equal(7, ordered[2].Value);
        Assert.Equal("zoe", ordered[3].Key);
        Assert.Equal(5, ordered[3].Value);
        Assert.Equal("dave", ordered[4].Key);
        Assert.Equal(3, ordered[4].Value);
    }

    [Fact]
    public void Multiple_players_scoring_accumulates_correctly()
    {
        var state = new GameStateSnapshot(
            sessionCode: "TEST",
            phase: Phase.Guessing,
            songIndex: 1,
            currentSong: null,
            choices: ["A", "B", "C"],
            hintIndex: 0,
            tallies: [0, 0, 0],
            scores: new Dictionary<string, int>
            {
                ["player1"] = 2,
                ["player2"] = 1,
                ["player3"] = 0
            },
            songStartedAtUtc: null);

        // All players submit correct answer (index 1)
        (state, _) = GameReducer.Reduce(state, new AnswerSubmitted("player1", 1)
        {
            AudienceId = "player1",
            ChoiceIndex = 1,
            SessionCode = state.SessionCode,
            EmittedAtUtc = DateTime.UtcNow,
            CorrelationId = Guid.Empty,
            CausedByCommandId = Guid.Empty
        });

        (state, _) = GameReducer.Reduce(state, new AnswerSubmitted("player2", 1)
        {
            AudienceId = "player2",
            ChoiceIndex = 1,
            SessionCode = state.SessionCode,
            EmittedAtUtc = DateTime.UtcNow,
            CorrelationId = Guid.Empty,
            CausedByCommandId = Guid.Empty
        });

        (state, _) = GameReducer.Reduce(state, new AnswerSubmitted("player3", 1)
        {
            AudienceId = "player3",
            ChoiceIndex = 1,
            SessionCode = state.SessionCode,
            EmittedAtUtc = DateTime.UtcNow,
            CorrelationId = Guid.Empty,
            CausedByCommandId = Guid.Empty
        });

        // Reveal correct answer
        (state, _) = GameReducer.Reduce(state, new CorrectAnswerRevealed(1)
        {
            CorrectChoiceIndex = 1,
            SessionCode = state.SessionCode,
            EmittedAtUtc = DateTime.UtcNow,
            CorrelationId = Guid.Empty,
            CausedByCommandId = Guid.Empty
        });

        // All players should get +1
        Assert.Equal(3, state.Scores["player1"]); // 2 + 1
        Assert.Equal(2, state.Scores["player2"]); // 1 + 1
        Assert.Equal(1, state.Scores["player3"]); // 0 + 1
    }

    [Fact]
    public void Ties_handled_deterministically_across_multiple_reveals()
    {
        var state = new GameStateSnapshot(
            sessionCode: "TEST",
            phase: Phase.Guessing,
            songIndex: 1,
            currentSong: null,
            choices: ["A", "B"],
            hintIndex: 0,
            tallies: [0, 0],
            scores: null,
            songStartedAtUtc: null);

        // First song: alice and bob both correct
        (state, _) = GameReducer.Reduce(state, new AnswerSubmitted("alice", 0)
        {
            AudienceId = "alice",
            ChoiceIndex = 0,
            SessionCode = state.SessionCode,
            EmittedAtUtc = DateTime.UtcNow,
            CorrelationId = Guid.Empty,
            CausedByCommandId = Guid.Empty
        });

        (state, _) = GameReducer.Reduce(state, new AnswerSubmitted("bob", 0)
        {
            AudienceId = "bob",
            ChoiceIndex = 0,
            SessionCode = state.SessionCode,
            EmittedAtUtc = DateTime.UtcNow,
            CorrelationId = Guid.Empty,
            CausedByCommandId = Guid.Empty
        });

        (state, _) = GameReducer.Reduce(state, new CorrectAnswerRevealed(0)
        {
            CorrectChoiceIndex = 0,
            SessionCode = state.SessionCode,
            EmittedAtUtc = DateTime.UtcNow,
            CorrelationId = Guid.Empty,
            CausedByCommandId = Guid.Empty
        });

        // Both have 1 point - tie
        Assert.Equal(1, state.Scores["alice"]);
        Assert.Equal(1, state.Scores["bob"]);

        // Verify deterministic ordering: alice < bob alphabetically
        var ordered1 = state.Scores.OrderByDescending(kvp => kvp.Value).ThenBy(kvp => kvp.Key, StringComparer.Ordinal).ToList();
        Assert.Equal("alice", ordered1[0].Key);
        Assert.Equal("bob", ordered1[1].Key);

        // Second song: reset answers, both correct again
        (state, _) = GameReducer.Reduce(state, new GamePhaseChanged(Phase.Guessing, Phase.Start)
        {
            CurrentPhase = Phase.Guessing,
            NewPhase = Phase.Start,
            SessionCode = state.SessionCode,
            EmittedAtUtc = DateTime.UtcNow,
            CorrelationId = Guid.Empty,
            CausedByCommandId = Guid.Empty
        });

        state = state with { Phase = Phase.Guessing, Choices = ["A", "B"], Tallies = [0, 0] };

        (state, _) = GameReducer.Reduce(state, new AnswerSubmitted("alice", 1)
        {
            AudienceId = "alice",
            ChoiceIndex = 1,
            SessionCode = state.SessionCode,
            EmittedAtUtc = DateTime.UtcNow,
            CorrelationId = Guid.Empty,
            CausedByCommandId = Guid.Empty
        });

        (state, _) = GameReducer.Reduce(state, new AnswerSubmitted("bob", 1)
        {
            AudienceId = "bob",
            ChoiceIndex = 1,
            SessionCode = state.SessionCode,
            EmittedAtUtc = DateTime.UtcNow,
            CorrelationId = Guid.Empty,
            CausedByCommandId = Guid.Empty
        });

        (state, _) = GameReducer.Reduce(state, new CorrectAnswerRevealed(1)
        {
            CorrectChoiceIndex = 1,
            SessionCode = state.SessionCode,
            EmittedAtUtc = DateTime.UtcNow,
            CorrelationId = Guid.Empty,
            CausedByCommandId = Guid.Empty
        });

        // Both now have 2 points - still tied, ordering should remain stable
        Assert.Equal(2, state.Scores["alice"]);
        Assert.Equal(2, state.Scores["bob"]);

        var ordered2 = state.Scores.OrderByDescending(kvp => kvp.Value).ThenBy(kvp => kvp.Key, StringComparer.Ordinal).ToList();
        Assert.Equal("alice", ordered2[0].Key);
        Assert.Equal("bob", ordered2[1].Key);
    }
}
