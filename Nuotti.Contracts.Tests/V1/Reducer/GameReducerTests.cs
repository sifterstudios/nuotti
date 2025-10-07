﻿using Nuotti.Contracts.V1.Enum;
using Nuotti.Contracts.V1.Event;
using Nuotti.Contracts.V1.Model;
using Nuotti.Contracts.V1.Reducer;

namespace Nuotti.Contracts.Tests.V1.Reducer;

public class GameReducerTests
{
    [Fact]
    public void Valid_path_Lobby_to_Intermission_succeeds()
    {
        var state = GameReducer.Initial("SESS-1");

        // Lobby -> Start
        (state, var err) = GameReducer.Reduce(state, new GamePhaseChanged(Phase.Lobby, Phase.Start)
        {
            CurrentPhase = Phase.Lobby,
            NewPhase = Phase.Start,
            SessionCode = state.SessionCode,
            EmittedAtUtc = DateTime.UtcNow,
            CorrelationId = Guid.Empty,
            CausedByCommandId = Guid.Empty
        });
        Assert.Null(err);
        Assert.Equal(Phase.Start, state.Phase);

        // Start -> Play
        (state, err) = GameReducer.Reduce(state, new GamePhaseChanged(Phase.Start, Phase.Play)
        {
            CurrentPhase = Phase.Start,
            NewPhase = Phase.Play,
            SessionCode = state.SessionCode,
            EmittedAtUtc = DateTime.UtcNow,
            CorrelationId = Guid.Empty,
            CausedByCommandId = Guid.Empty
        });
        Assert.Null(err);
        Assert.Equal(Phase.Play, state.Phase);

        // Play -> Guessing
        (state, err) = GameReducer.Reduce(state, new GamePhaseChanged(Phase.Play, Phase.Guessing)
        {
            CurrentPhase = Phase.Play,
            NewPhase = Phase.Guessing,
            SessionCode = state.SessionCode,
            EmittedAtUtc = DateTime.UtcNow,
            CorrelationId = Guid.Empty,
            CausedByCommandId = Guid.Empty
        });
        Assert.Null(err);
        Assert.Equal(Phase.Guessing, state.Phase);

        // Guessing -> Reveal
        (state, err) = GameReducer.Reduce(state, new GamePhaseChanged(Phase.Guessing, Phase.Reveal)
        {
            CurrentPhase = Phase.Guessing,
            NewPhase = Phase.Reveal,
            SessionCode = state.SessionCode,
            EmittedAtUtc = DateTime.UtcNow,
            CorrelationId = Guid.Empty,
            CausedByCommandId = Guid.Empty
        });
        Assert.Null(err);
        Assert.Equal(Phase.Reveal, state.Phase);

        // Reveal -> Intermission
        (state, err) = GameReducer.Reduce(state, new GamePhaseChanged(Phase.Reveal, Phase.Intermission)
        {
            CurrentPhase = Phase.Reveal,
            NewPhase = Phase.Intermission,
            SessionCode = state.SessionCode,
            EmittedAtUtc = DateTime.UtcNow,
            CorrelationId = Guid.Empty,
            CausedByCommandId = Guid.Empty
        });
        Assert.Null(err);
        Assert.Equal(Phase.Intermission, state.Phase);
    }

    [Fact]
    public void Reducer_trusts_event_legality_and_applies_change_when_phase_matches()
    {
        var state = GameReducer.Initial("SESS-1");

        var evt = new GamePhaseChanged(Phase.Lobby, Phase.Guessing)
        {
            CurrentPhase = Phase.Lobby,
            NewPhase = Phase.Guessing,
            SessionCode = state.SessionCode,
            EmittedAtUtc = DateTime.UtcNow,
            CorrelationId = Guid.Empty,
            CausedByCommandId = Guid.Empty
        };

        var (next, err) = GameReducer.Reduce(state, evt);

        Assert.Null(err);
        Assert.Equal(Phase.Guessing, next.Phase);
    }

    [Fact]
    public void AnswerSubmitted_increments_tally_during_Guessing()
    {
        var state = new GameStateSnapshot(
            sessionCode: "S",
            phase: Phase.Guessing,
            songIndex: 1,
            currentSong: null,
            choices: new[] { "A", "B", "C" },
            hintIndex: 0,
            tallies: new[] { 0, 0, 0 },
            scores: null,
            songStartedAtUtc: null);

        (state, var err) = GameReducer.Reduce(state, new AnswerSubmitted("aud-1", 1)
        {
            AudienceId = "aud-1",
            ChoiceIndex = 1,
            SessionCode = state.SessionCode,
            EmittedAtUtc = DateTime.UtcNow,
            CorrelationId = Guid.Empty,
            CausedByCommandId = Guid.Empty
        });

        Assert.Null(err);
        Assert.Equal(new[] { 0, 1, 0 }, state.Tallies);
    }

    [Fact]
    public void AnswerSubmitted_ignored_when_not_Guessing()
    {
        var state = new GameStateSnapshot(
            sessionCode: "S",
            phase: Phase.Lobby,
            songIndex: 0,
            currentSong: null,
            choices: new[] { "A", "B" },
            hintIndex: 0,
            tallies: new[] { 0, 0 },
            scores: null,
            songStartedAtUtc: null);

        var before = state.Tallies.ToArray();

        var (next, err) = GameReducer.Reduce(state, new AnswerSubmitted("aud-1", 0)
        {
            AudienceId = "aud-1",
            ChoiceIndex = 0,
            SessionCode = state.SessionCode,
            EmittedAtUtc = DateTime.UtcNow,
            CorrelationId = Guid.Empty,
            CausedByCommandId = Guid.Empty
        });

        Assert.Null(err);
        Assert.Equal(before, next.Tallies);
        Assert.Equal(Phase.Lobby, next.Phase);
    }

    [Fact]
    public void Tallies_reset_on_next_song_phase_change_to_Start()
    {
        var state = new GameStateSnapshot(
            sessionCode: "S",
            phase: Phase.Guessing,
            songIndex: 3,
            currentSong: null,
            choices: new[] { "A", "B" },
            hintIndex: 0,
            tallies: new[] { 2, 5 },
            scores: null,
            songStartedAtUtc: null);

        var (next, err) = GameReducer.Reduce(state, new GamePhaseChanged(Phase.Guessing, Phase.Start)
        {
            CurrentPhase = Phase.Guessing,
            NewPhase = Phase.Start,
            SessionCode = state.SessionCode,
            EmittedAtUtc = DateTime.UtcNow,
            CorrelationId = Guid.Empty,
            CausedByCommandId = Guid.Empty
        });

        Assert.Null(err);
        Assert.Equal(Phase.Start, next.Phase);
        Assert.Empty(next.Tallies);
    }
}
