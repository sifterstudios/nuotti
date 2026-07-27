namespace Nuotti.Contracts.V1.Model;

/// <summary>
/// One row of a scoreboard, ordered highest score first.
/// </summary>
/// <param name="Player">Player identifier as held in <see cref="GameStateSnapshot.Scores"/>.</param>
/// <param name="Score">Cumulative score.</param>
/// <param name="Rank">1-based position after ordering.</param>
public sealed record ScoreRow(string Player, int Score, int Rank);

/// <summary>
/// Derived display values over <see cref="GameStateSnapshot"/>.
/// </summary>
/// <remarks>
/// These live beside the record rather than on it. The snapshot is serialized under two naming
/// policies and mirrored by hand in web/shared/contracts.ts, so a computed property added to the
/// record is one forgotten [JsonIgnore] away from changing the wire format. Keeping derivation in
/// extension methods makes that impossible.
///
/// Each client used to carry its own copy of these: Projector in Models/GameState, Performer in
/// PerformerUiState, Audience inline in Question.razor.
/// </remarks>
public static class GameStateSnapshotViews
{
    const string UnknownTitle = "Unknown Song";
    const string UnknownArtist = "Unknown Artist";

    public static string SongTitle(this GameStateSnapshot state)
        => state.CurrentSong?.Title ?? UnknownTitle;

    public static string SongArtist(this GameStateSnapshot state)
        => state.CurrentSong?.Artist ?? UnknownArtist;

    public static string SongDisplay(this GameStateSnapshot state)
        => $"{state.SongTitle()} - {state.SongArtist()}";

    public static bool HasChoices(this GameStateSnapshot state)
        => state.Choices.Count > 0;

    public static bool HasTallies(this GameStateSnapshot state)
        => state.Tallies.Count > 0;

    public static bool HasScores(this GameStateSnapshot state)
        => state.Scores.Count > 0;

    /// <summary>Total answers submitted for the current song.</summary>
    public static int TotalAnswers(this GameStateSnapshot state)
        => state.Tallies.Sum();

    /// <summary>The hint number to show a human: 1-based, never below 1.</summary>
    public static int CurrentHintNumber(this GameStateSnapshot state)
        => Math.Max(1, state.HintIndex + 1);

    /// <summary>
    /// The top <paramref name="count"/> players, highest score first. Ties keep dictionary order.
    /// </summary>
    public static IReadOnlyList<ScoreRow> TopPlayers(this GameStateSnapshot state, int count = 10)
        => state.Scores
            .OrderByDescending(kvp => kvp.Value)
            .Take(count)
            .Select((kvp, i) => new ScoreRow(kvp.Key, kvp.Value, i + 1))
            .ToArray();
}
