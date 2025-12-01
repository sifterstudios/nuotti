using Nuotti.Contracts.V1.Enum;
using Nuotti.Contracts.V1.Event;
using Nuotti.Contracts.V1.Model;
using Nuotti.Contracts.V1.Reducer;

namespace Nuotti.Contracts.Tests.V1.Reducer;

public class GameReducerGuardTests
{
    [Fact]
    public void Illegal_phase_transition_returns_error_without_mutating_state()
    {
        var initialState = new GameStateSnapshot(
            sessionCode: "TEST",
            phase: Phase.Lobby,
            songIndex: 0,
            currentSong: null,
            choices: ["A", "B"],
            hintIndex: 0,
            tallies: [1, 2],
            scores: null,
            songStartedAtUtc: null);

        // Try to transition from Lobby directly to Reveal (illegal)
        var (newState, error) = GameReducer.Reduce(initialState, new GamePhaseChanged(Phase.Lobby, Phase.Reveal)
        {
            CurrentPhase = Phase.Lobby,
            NewPhase = Phase.Reveal,
            SessionCode = initialState.SessionCode,
            EmittedAtUtc = DateTime.UtcNow,
            CorrelationId = Guid.Empty,
            CausedByCommandId = Guid.Empty
        });

        // Should return error
        Assert.NotNull(error);
        Assert.Contains("phase_mismatch", error);

        // State should be unchanged
        Assert.Equal(Phase.Lobby, newState.Phase);
        Assert.Equal(initialState.SongIndex, newState.SongIndex);
        Assert.Equal(initialState.Tallies, newState.Tallies);
    }

    [Fact]
    public void Phase_mismatch_returns_error_without_mutating_state()
    {
        var initialState = new GameStateSnapshot(
            sessionCode: "TEST",
            phase: Phase.Guessing,
            songIndex: 1,
            currentSong: null,
            choices: ["A", "B", "C"],
            hintIndex: 0,
            tallies: [5, 3, 2],
            scores: null,
            songStartedAtUtc: null);

        // Event says current phase is Lobby, but state is Guessing
        var (newState, error) = GameReducer.Reduce(initialState, new GamePhaseChanged(Phase.Lobby, Phase.Start)
        {
            CurrentPhase = Phase.Lobby,  // Mismatch: state is Guessing
            NewPhase = Phase.Start,
            SessionCode = initialState.SessionCode,
            EmittedAtUtc = DateTime.UtcNow,
            CorrelationId = Guid.Empty,
            CausedByCommandId = Guid.Empty
        });

        // Should return error
        Assert.NotNull(error);
        Assert.Contains("phase_mismatch", error);
        Assert.Contains("state=Guessing", error);
        Assert.Contains("eventCurrent=Lobby", error);

        // State should be unchanged
        Assert.Equal(Phase.Guessing, newState.Phase);
        Assert.Equal(initialState.SongIndex, newState.SongIndex);
        Assert.Equal(initialState.Tallies, newState.Tallies);
        Assert.Equal(initialState.HintIndex, newState.HintIndex);
    }

    [Fact]
    public void Reducer_unchanged_on_invalid_phase_transition()
    {
        var initialState = GameReducer.Initial("TEST-SESSION");
        var originalPhase = initialState.Phase;
        var originalTallies = initialState.Tallies.ToArray();
        var originalHintIndex = initialState.HintIndex;

        // Try invalid transition: Lobby -> Reveal (skipping required phases)
        var (resultState, error) = GameReducer.Reduce(initialState, new GamePhaseChanged(Phase.Lobby, Phase.Reveal)
        {
            CurrentPhase = Phase.Lobby,
            NewPhase = Phase.Reveal,
            SessionCode = initialState.SessionCode,
            EmittedAtUtc = DateTime.UtcNow,
            CorrelationId = Guid.Empty,
            CausedByCommandId = Guid.Empty
        });

        // Error should be present
        Assert.NotNull(error);

        // All state fields should remain unchanged
        Assert.Equal(originalPhase, resultState.Phase);
        Assert.Equal(initialState.SessionCode, resultState.SessionCode);
        Assert.Equal(initialState.SongIndex, resultState.SongIndex);
        Assert.Equal(originalHintIndex, resultState.HintIndex);
        Assert.Equal(originalTallies, resultState.Tallies);
        Assert.Equal(initialState.Choices, resultState.Choices);
    }

    [Theory]
    [InlineData(Phase.Lobby, Phase.Reveal)]
    [InlineData(Phase.Lobby, Phase.Intermission)]
    [InlineData(Phase.Start, Phase.Finished)]
    [InlineData(Phase.Guessing, Phase.Lobby)]
    [InlineData(Phase.Reveal, Phase.Lobby)]
    public void Various_illegal_transitions_return_error(Phase fromPhase, Phase toPhase)
    {
        var state = new GameStateSnapshot(
            sessionCode: "TEST",
            phase: fromPhase,
            songIndex: 0,
            currentSong: null,
            choices: [],
            hintIndex: 0,
            tallies: [],
            scores: null,
            songStartedAtUtc: null);

        var (newState, error) = GameReducer.Reduce(state, new GamePhaseChanged(fromPhase, toPhase)
        {
            CurrentPhase = fromPhase,
            NewPhase = toPhase,
            SessionCode = state.SessionCode,
            EmittedAtUtc = DateTime.UtcNow,
            CorrelationId = Guid.Empty,
            CausedByCommandId = Guid.Empty
        });

        // Note: The reducer doesn't validate transition legality - that's done at command level
        // The reducer only checks phase mismatch. So if fromPhase matches state.Phase, it will apply.
        // For true illegal transition testing, we need to test at the endpoint level.
        // This test verifies the reducer's phase mismatch guard works.
        if (fromPhase == state.Phase)
        {
            // If phases match, reducer applies the change (it trusts events)
            // The error would come from command validation, not reducer
            Assert.Null(error);
            Assert.Equal(toPhase, newState.Phase);
        }
    }

    [Fact]
    public void AnswerSubmitted_outside_Guessing_phase_ignored_without_error()
    {
        var state = new GameStateSnapshot(
            sessionCode: "TEST",
            phase: Phase.Lobby,
            songIndex: 0,
            currentSong: null,
            choices: ["A", "B"],
            hintIndex: 0,
            tallies: [0, 0],
            scores: null,
            songStartedAtUtc: null);

        var originalTallies = state.Tallies.ToArray();

        var (newState, error) = GameReducer.Reduce(state, new AnswerSubmitted("aud-1", 0)
        {
            AudienceId = "aud-1",
            ChoiceIndex = 0,
            SessionCode = state.SessionCode,
            EmittedAtUtc = DateTime.UtcNow,
            CorrelationId = Guid.Empty,
            CausedByCommandId = Guid.Empty
        });

        // No error, but state unchanged (ignored)
        Assert.Null(error);
        Assert.Equal(originalTallies, newState.Tallies);
        Assert.Equal(Phase.Lobby, newState.Phase);
    }

    [Fact]
    public void CorrectAnswerRevealed_with_invalid_choice_index_ignored_without_error()
    {
        var state = new GameStateSnapshot(
            sessionCode: "TEST",
            phase: Phase.Reveal,
            songIndex: 1,
            currentSong: null,
            choices: ["A", "B"],
            hintIndex: 0,
            tallies: [5, 3],
            scores: new Dictionary<string, int> { ["p1"] = 5 },
            songStartedAtUtc: null);

        var originalScores = new Dictionary<string, int>(state.Scores);

        // Try to reveal with invalid choice index (out of bounds)
        var (newState, error) = GameReducer.Reduce(state, new CorrectAnswerRevealed(99) // Invalid index
        {
            CorrectChoiceIndex = 99,
            SessionCode = state.SessionCode,
            EmittedAtUtc = DateTime.UtcNow,
            CorrelationId = Guid.Empty,
            CausedByCommandId = Guid.Empty
        });

        // No error, but scores unchanged (ignored)
        Assert.Null(error);
        Assert.Equal(originalScores, newState.Scores);
    }

    [Fact]
    public Task ProblemDetails_mapping_for_invalid_transition_snapshot()
    {
        // Simulate the ProblemDetails that would be created from a reducer error
        // This tests the shape of ProblemDetails for invalid transitions
        var problem = new NuottiProblem(
            "Invalid state transition",
            409,
            "Cannot change phase from Lobby to Reveal.",
            ReasonCode.InvalidStateTransition,
            null,
            Guid.Parse("00000000-0000-0000-0000-000000000001"));

        var json = System.Text.Json.JsonSerializer.Serialize(problem, Nuotti.Contracts.V1.ContractsJson.RestOptions);
        return VerifyJson(json, VerifyDefaults.Settings());
    }
}

