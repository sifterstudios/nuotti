namespace Nuotti.Backend.Models;

public class Song
{
    public string Id { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string Artist { get; set; } = string.Empty;
    public string BackingTrackPath { get; set; } = string.Empty;
    public string MetronomeTrackPath { get; set; } = string.Empty;
    public List<LyricLine> Lyrics { get; set; } = new();
    public TimeSpan Duration { get; set; }
}

public class LyricLine
{
    public string Text { get; set; } = string.Empty;
    public TimeSpan StartTime { get; set; }
    public TimeSpan EndTime { get; set; }
} 