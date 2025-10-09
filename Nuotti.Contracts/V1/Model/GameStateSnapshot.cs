using Nuotti.Contracts.V1.Enum;
using System.Collections.Frozen;
using System.Text.Json.Serialization;
namespace Nuotti.Contracts.V1.Model;

/// <summary>
/// Immutable snapshot of the current game state intended as the single source of truth for the UI.
/// </summary>
public sealed record GameStateSnapshot
{
    /// <summary>
    /// The session code identifying the current game instance.
    /// </summary>
    public string SessionCode { get; init; }

    /// <summary>
    /// The current game phase.
    /// </summary>
    public Phase Phase { get; init; }

    /// <summary>
    /// Zero-based index of the current song within the game/round.
    /// </summary>
    public int SongIndex { get; init; }

    /// <summary>
    /// The currently active song reference if available; otherwise null.
    /// </summary>
    public SongRef? CurrentSong { get; init; }

    /// <summary>
    /// Full catalog of songs available in this session for reference. Never null.
    /// </summary>
    public IReadOnlyList<SongRef> Catalog { get; init; } = [];

    /// <summary>
    /// The choices available to players for the current song. Never null.
    /// </summary>
    public IReadOnlyList<string> Choices { get; init; } = [];

    /// <summary>
    /// Index of the current hint revealed for the song.
    /// </summary>
    public int HintIndex { get; init; }

    /// <summary>
    /// Tally counts for each choice (parallel to <see cref="Choices"/>). Never null.
    /// </summary>
    public IReadOnlyList<int> Tallies { get; init; } = [];

    /// <summary>
    /// Cumulative score by player identifier. Never null.
    /// </summary>
    public IReadOnlyDictionary<string, int> Scores { get; init; } = FrozenDictionary<string, int>.Empty;

    /// <summary>
    /// Latest submitted answers per audience. Internal server-side bookkeeping; not serialized.
    /// </summary>
    [JsonIgnore]
    public IReadOnlyDictionary<string, int> Answers { get; init; } = FrozenDictionary<string, int>.Empty;

    /// <summary>
    /// UTC timestamp when the current song started, if applicable.
    /// </summary>
    public DateTime? SongStartedAtUtc { get; init; }

    /// <summary>
    /// JSON constructor to ensure backward compatibility and default non-null collections.
    /// Missing or null collections are replaced with empty collections.
    /// </summary>
    [JsonConstructor]
    public GameStateSnapshot(
        string sessionCode,
        Phase phase,
        int songIndex,
        SongRef? currentSong,
        IReadOnlyList<SongRef>? catalog,
        IReadOnlyList<string>? choices,
        int hintIndex,
        IReadOnlyList<int>? tallies,
        IReadOnlyDictionary<string, int>? scores,
        DateTime? songStartedAtUtc)
    {
        SessionCode = sessionCode;
        Phase = phase;
        SongIndex = songIndex;
        CurrentSong = currentSong;
        Catalog = catalog ?? [];
        Choices = choices ?? [];
        HintIndex = hintIndex;
        Tallies = tallies ?? [];
        Scores = scores ?? FrozenDictionary<string, int>.Empty;
        SongStartedAtUtc = songStartedAtUtc;
    }

    /// <summary>
    /// Creates a new snapshot programmatically with default empty collections where not provided.
    /// </summary>
    public GameStateSnapshot(
        string sessionCode,
        Phase phase,
        int songIndex,
        SongRef? currentSong = null,
        IEnumerable<SongRef>? catalog = null,
        IEnumerable<string>? choices = null,
        int hintIndex = 0,
        IEnumerable<int>? tallies = null,
        IReadOnlyDictionary<string, int>? scores = null,
        DateTime? songStartedAtUtc = null)
    {
        SessionCode = sessionCode;
        Phase = phase;
        SongIndex = songIndex;
        CurrentSong = currentSong;
        Catalog = (catalog ?? []).ToArray();
        Choices = (choices ?? []).ToArray();
        HintIndex = hintIndex;
        Tallies = (tallies ?? []).ToArray();
        Scores = scores ?? FrozenDictionary<string, int>.Empty;
        SongStartedAtUtc = songStartedAtUtc;
    }

    // Backward-compatible overload (without catalog parameter) to avoid breaking existing call sites
    public GameStateSnapshot(
        string sessionCode,
        Phase phase,
        int songIndex,
        SongRef? currentSong,
        IEnumerable<string>? choices = null,
        int hintIndex = 0,
        IEnumerable<int>? tallies = null,
        IReadOnlyDictionary<string, int>? scores = null,
        DateTime? songStartedAtUtc = null)
        : this(sessionCode, phase, songIndex, currentSong, catalog: null, choices, hintIndex, tallies, scores, songStartedAtUtc)
    { }
}