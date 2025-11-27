using Nuotti.Contracts.V1.Enum;
using Nuotti.Contracts.V1.Model;

namespace Nuotti.Projector.Models;

public class GameState
{
    public Phase Phase { get; set; } = Phase.Lobby;
    public string SessionCode { get; set; } = string.Empty;
    public int SongIndex { get; set; }
    public SongRef? CurrentSong { get; set; }
    public IReadOnlyList<string> Choices { get; set; } = [];
    public int HintIndex { get; set; }
    public IReadOnlyList<int> Tallies { get; set; } = [];
    public IReadOnlyDictionary<string, int> Scores { get; set; } = new Dictionary<string, int>();
    public IReadOnlyList<SongRef> Catalog { get; set; } = [];
    public DateTimeOffset? SongStartedAtUtc { get; set; }
    
    public string CurrentSongTitle => CurrentSong?.Title ?? "Unknown Song";
    public string CurrentSongArtist => CurrentSong?.Artist ?? "Unknown Artist";
    public string CurrentSongDisplay => $"{CurrentSongTitle} - {CurrentSongArtist}";
    
    public bool HasChoices => Choices.Count > 0;
    public bool HasTallies => Tallies.Count > 0;
    public bool HasScores => Scores.Count > 0;
    
    public int TotalAnswers => Tallies.Sum();
    
    public List<(string Player, int Score, int Change)> GetTopPlayers(int count = 10)
    {
        return Scores
            .OrderByDescending(kvp => kvp.Value)
            .Take(count)
            .Select(kvp => (kvp.Key, kvp.Value, 0)) // TODO: Track score changes
            .ToList();
    }
    
    public bool HasHints => HintIndex >= 0;
    public int CurrentHintNumber => Math.Max(1, HintIndex + 1);
}
