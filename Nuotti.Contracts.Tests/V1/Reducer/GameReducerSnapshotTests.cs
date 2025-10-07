using Nuotti.Contracts.V1;
using Nuotti.Contracts.V1.Enum;
using Nuotti.Contracts.V1.Event;
using Nuotti.Contracts.V1.Model;
using Nuotti.Contracts.V1.Reducer;
using System.Text.Json;
namespace Nuotti.Contracts.Tests.V1.Reducer;

public class GameReducerSnapshotTests
{
    private static string FixturesDir => Path.Combine(AppContext.BaseDirectory, "V1", "Reducer", "Fixtures");

    private static string LoadFixture(string name)
    {
        var path = Path.Combine(FixturesDir, name + ".json");
        return File.ReadAllText(path);
    }

    private static string Normalize(string json)
    {
        using var doc = JsonDocument.Parse(json);
        return JsonSerializer.Serialize(doc.RootElement, ContractsJson.DefaultOptions);
    }

    private static string Serialize(GameStateSnapshot state)
        => JsonSerializer.Serialize(state, ContractsJson.DefaultOptions);

    [Fact]
    public void Lobby_snapshot_matches_fixture()
    {
        var state = GameReducer.Initial("SESS");
        var expected = Normalize(LoadFixture("Lobby"));
        var actual = Normalize(Serialize(state));
        Assert.Equal(expected, actual);
    }

    [Fact]
    public void Start_snapshot_matches_fixture()
    {
        var state = GameReducer.Initial("SESS");
        (state, var err) = GameReducer.Reduce(state, new GamePhaseChanged(Phase.Lobby, Phase.Start)
        {
            CurrentPhase = Phase.Lobby,
            NewPhase = Phase.Start,
            SessionCode = state.SessionCode,
            EmittedAtUtc = DateTime.UnixEpoch,
            CorrelationId = Guid.Empty,
            CausedByCommandId = Guid.Empty
        });
        Assert.Null(err);
        var expected = Normalize(LoadFixture("Start"));
        var actual = Normalize(Serialize(state));
        Assert.Equal(expected, actual);
    }

    [Fact]
    public void Play_snapshot_matches_fixture()
    {
        var state = GameReducer.Initial("SESS");
        (state, _) = GameReducer.Reduce(state, new GamePhaseChanged(Phase.Lobby, Phase.Start)
        {
            CurrentPhase = Phase.Lobby,
            NewPhase = Phase.Start,
            SessionCode = state.SessionCode,
            EmittedAtUtc = DateTime.UnixEpoch,
            CorrelationId = Guid.Empty,
            CausedByCommandId = Guid.Empty
        });
        (state, var err) = GameReducer.Reduce(state, new GamePhaseChanged(Phase.Start, Phase.Play)
        {
            CurrentPhase = Phase.Start,
            NewPhase = Phase.Play,
            SessionCode = state.SessionCode,
            EmittedAtUtc = DateTime.UnixEpoch,
            CorrelationId = Guid.Empty,
            CausedByCommandId = Guid.Empty
        });
        Assert.Null(err);
        var expected = Normalize(LoadFixture("Play"));
        var actual = Normalize(Serialize(state));
        Assert.Equal(expected, actual);
    }

    [Fact]
    public void Guessing_snapshot_matches_fixture()
    {
        var state = GameReducer.Initial("SESS");
        (state, _) = GameReducer.Reduce(state, new GamePhaseChanged(Phase.Lobby, Phase.Start)
        {
            CurrentPhase = Phase.Lobby,
            NewPhase = Phase.Start,
            SessionCode = state.SessionCode,
            EmittedAtUtc = DateTime.UnixEpoch,
            CorrelationId = Guid.Empty,
            CausedByCommandId = Guid.Empty
        });
        (state, var err) = GameReducer.Reduce(state, new GamePhaseChanged(Phase.Start, Phase.Play)
        {
            CurrentPhase = Phase.Start,
            NewPhase = Phase.Play,
            SessionCode = state.SessionCode,
            EmittedAtUtc = DateTime.UnixEpoch,
            CorrelationId = Guid.Empty,
            CausedByCommandId = Guid.Empty
        });
        Assert.Null(err);
        (state, err) = GameReducer.Reduce(state, new GamePhaseChanged(Phase.Play, Phase.Guessing)
        {
            CurrentPhase = Phase.Play,
            NewPhase = Phase.Guessing,
            SessionCode = state.SessionCode,
            EmittedAtUtc = DateTime.UnixEpoch,
            CorrelationId = Guid.Empty,
            CausedByCommandId = Guid.Empty
        });
        Assert.Null(err);
        var expected = Normalize(LoadFixture("Guessing"));
        var actual = Normalize(Serialize(state));
        Assert.Equal(expected, actual);
    }

    [Fact]
    public void Reveal_snapshot_matches_fixture()
    {
        var state = GameReducer.Initial("SESS");
        (state, _) = GameReducer.Reduce(state, new GamePhaseChanged(Phase.Lobby, Phase.Start)
        {
            CurrentPhase = Phase.Lobby,
            NewPhase = Phase.Start,
            SessionCode = state.SessionCode,
            EmittedAtUtc = DateTime.UnixEpoch,
            CorrelationId = Guid.Empty,
            CausedByCommandId = Guid.Empty
        });
        (state, _) = GameReducer.Reduce(state, new GamePhaseChanged(Phase.Start, Phase.Play)
        {
            CurrentPhase = Phase.Start,
            NewPhase = Phase.Play,
            SessionCode = state.SessionCode,
            EmittedAtUtc = DateTime.UnixEpoch,
            CorrelationId = Guid.Empty,
            CausedByCommandId = Guid.Empty
        });
        (state, var err) = GameReducer.Reduce(state, new GamePhaseChanged(Phase.Play, Phase.Guessing)
        {
            CurrentPhase = Phase.Play,
            NewPhase = Phase.Guessing,
            SessionCode = state.SessionCode,
            EmittedAtUtc = DateTime.UnixEpoch,
            CorrelationId = Guid.Empty,
            CausedByCommandId = Guid.Empty
        });
        Assert.Null(err);
        (state, err) = GameReducer.Reduce(state, new GamePhaseChanged(Phase.Guessing, Phase.Reveal)
        {
            CurrentPhase = Phase.Guessing,
            NewPhase = Phase.Reveal,
            SessionCode = state.SessionCode,
            EmittedAtUtc = DateTime.UnixEpoch,
            CorrelationId = Guid.Empty,
            CausedByCommandId = Guid.Empty
        });
        Assert.Null(err);
        var expected = Normalize(LoadFixture("Reveal"));
        var actual = Normalize(Serialize(state));
        Assert.Equal(expected, actual);
    }

    [Fact]
    public void Intermission_snapshot_matches_fixture()
    {
        var state = GameReducer.Initial("SESS");
        (state, _) = GameReducer.Reduce(state, new GamePhaseChanged(Phase.Lobby, Phase.Start)
        {
            CurrentPhase = Phase.Lobby,
            NewPhase = Phase.Start,
            SessionCode = state.SessionCode,
            EmittedAtUtc = DateTime.UnixEpoch,
            CorrelationId = Guid.Empty,
            CausedByCommandId = Guid.Empty
        });
        (state, _) = GameReducer.Reduce(state, new GamePhaseChanged(Phase.Start, Phase.Play)
        {
            CurrentPhase = Phase.Start,
            NewPhase = Phase.Play,
            SessionCode = state.SessionCode,
            EmittedAtUtc = DateTime.UnixEpoch,
            CorrelationId = Guid.Empty,
            CausedByCommandId = Guid.Empty
        });
        (state, _) = GameReducer.Reduce(state, new GamePhaseChanged(Phase.Play, Phase.Guessing)
        {
            CurrentPhase = Phase.Play,
            NewPhase = Phase.Guessing,
            SessionCode = state.SessionCode,
            EmittedAtUtc = DateTime.UnixEpoch,
            CorrelationId = Guid.Empty,
            CausedByCommandId = Guid.Empty
        });
        (state, _) = GameReducer.Reduce(state, new GamePhaseChanged(Phase.Guessing, Phase.Reveal)
        {
            CurrentPhase = Phase.Guessing,
            NewPhase = Phase.Reveal,
            SessionCode = state.SessionCode,
            EmittedAtUtc = DateTime.UnixEpoch,
            CorrelationId = Guid.Empty,
            CausedByCommandId = Guid.Empty
        });
        (state, var err) = GameReducer.Reduce(state, new GamePhaseChanged(Phase.Reveal, Phase.Intermission)
        {
            CurrentPhase = Phase.Reveal,
            NewPhase = Phase.Intermission,
            SessionCode = state.SessionCode,
            EmittedAtUtc = DateTime.UnixEpoch,
            CorrelationId = Guid.Empty,
            CausedByCommandId = Guid.Empty
        });
        Assert.Null(err);
        var expected = Normalize(LoadFixture("Intermission"));
        var actual = Normalize(Serialize(state));
        Assert.Equal(expected, actual);
    }
}
