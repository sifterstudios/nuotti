using Nuotti.Contracts.V1.Enum;
using Nuotti.Contracts.V1.Model;
using System.Collections.Frozen;

namespace Nuotti.UnitTests.TestHelpers;

/// <summary>
/// Fluent builder for creating test GameStateSnapshot instances with sensible defaults.
/// </summary>
public class GameStateBuilder
{
    private string _sessionCode = "TEST-SESSION";
    private Phase _phase = Phase.Lobby;
    private int _songIndex = 0;
    private SongRef? _currentSong = null;
    private IReadOnlyList<SongRef> _catalog = [];
    private IReadOnlyList<string> _choices = [];
    private int _hintIndex = 0;
    private IReadOnlyList<int> _tallies = [];
    private IReadOnlyDictionary<string, int> _scores = FrozenDictionary<string, int>.Empty;
    private DateTime? _songStartedAtUtc = null;

    public GameStateBuilder WithSessionCode(string sessionCode)
    {
        _sessionCode = sessionCode;
        return this;
    }

    public GameStateBuilder WithPhase(Phase phase)
    {
        _phase = phase;
        return this;
    }

    public GameStateBuilder WithSongIndex(int songIndex)
    {
        _songIndex = songIndex;
        return this;
    }

    public GameStateBuilder WithCurrentSong(SongRef? currentSong)
    {
        _currentSong = currentSong;
        return this;
    }

    public GameStateBuilder WithCatalog(IEnumerable<SongRef> catalog)
    {
        _catalog = catalog.ToArray();
        return this;
    }

    public GameStateBuilder WithChoices(IEnumerable<string> choices)
    {
        _choices = choices.ToArray();
        return this;
    }

    public GameStateBuilder WithHintIndex(int hintIndex)
    {
        _hintIndex = hintIndex;
        return this;
    }

    public GameStateBuilder WithTallies(IEnumerable<int> tallies)
    {
        _tallies = tallies.ToArray();
        return this;
    }

    public GameStateBuilder WithScores(IReadOnlyDictionary<string, int> scores)
    {
        _scores = scores;
        return this;
    }

    public GameStateBuilder WithSongStartedAtUtc(DateTime? songStartedAtUtc)
    {
        _songStartedAtUtc = songStartedAtUtc;
        return this;
    }

    public GameStateSnapshot Build()
    {
        return new GameStateSnapshot(
            sessionCode: _sessionCode,
            phase: _phase,
            songIndex: _songIndex,
            currentSong: _currentSong,
            catalog: _catalog,
            choices: _choices,
            hintIndex: _hintIndex,
            tallies: _tallies,
            scores: _scores,
            songStartedAtUtc: _songStartedAtUtc
        );
    }
}

