using Nuotti.Contracts.V1.Enum;
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
}
