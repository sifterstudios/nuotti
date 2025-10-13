using System.Text.Json.Serialization;
namespace Nuotti.AudioEngine;

public sealed class EngineOptions
{
    public PreferredPlayer PreferredPlayer { get; set; } = PreferredPlayer.Auto;
    public string? OutputBackend { get; set; }
    public string? OutputDevice { get; set; }
    public RoutingOptions Routing { get; set; } = new();
    public ClickOptions Click { get; set; } = new();
    public LogLevel LogLevel { get; set; } = LogLevel.Information;

    public void Validate()
    {
        // Ensure Routing present and arrays initialized (can be empty)
        if (Routing is null) throw new ArgumentException("Routing section is required");
        if (Routing.Tracks is null) throw new ArgumentException("Routing.Tracks must be specified (can be empty array)");
        if (Routing.Click is null) throw new ArgumentException("Routing.Click must be specified (can be empty array)");
        // Validate click options
        if (Click is null) throw new ArgumentException("Click section is required");
        if (Click.Level is < 0 or > 1) throw new ArgumentException("Click.Level must be between 0 and 1 inclusive");
        if (Click.Bpm <= 0) throw new ArgumentException("Click.Bpm must be positive");
        // OutputBackend/OutputDevice optional for now.
    }
}

public sealed class RoutingOptions
{
    // 1-based channel indices mapping logical buses to physical channels.
    public int[] Tracks { get; set; } = Array.Empty<int>();
    public int[] Click { get; set; } = Array.Empty<int>();
}

public sealed class ClickOptions
{
    // Overall level/volume 0..1 (0 disables click)
    public double Level { get; set; } = 0.0;
    // Tempo in BPM
    public int Bpm { get; set; } = 120;
}

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum PreferredPlayer
{
    Auto,
    Afplay,
    Ffplay,
    Vlc
}

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum LogLevel
{
    Trace,
    Debug,
    Information,
    Warning,
    Error,
    Critical,
    None
}
