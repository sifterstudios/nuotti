namespace Nuotti.Backend.Models;

public enum GamePhase
{
    Waiting,    // Waiting for players to join
    Hinting,    // Band is giving hints
    Guessing,   // Audience is guessing
    Playing,    // Song is playing
    Results     // Showing results
}

public class GameState
{
    public GamePhase CurrentPhase { get; set; } = GamePhase.Waiting;
    public Song? CurrentSong { get; set; }
    public Dictionary<string, Player> Players { get; set; } = new();
    public DateTime? GameStartTime { get; set; }
    public TimeSpan? CurrentPlaybackPosition { get; set; }
    public bool IsPlaying { get; set; }
} 