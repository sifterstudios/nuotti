using System.Text.Json.Serialization;
namespace Nuotti.AudioEngine;

public sealed class EngineOptions
{
    public PreferredPlayer PreferredPlayer { get; set; } = PreferredPlayer.Auto;
    public string? OutputBackend { get; set; }
    public string? OutputDevice { get; set; }
    public RoutingOptions Routing { get; set; } = new();
    public LogLevel LogLevel { get; set; } = LogLevel.Information;

    public void Validate()
    {
        // Ensure Routing present and arrays initialized (can be empty)
        if (Routing is null) throw new ArgumentException("Routing section is required");
        if (Routing.Tracks is null) throw new ArgumentException("Routing.Tracks must be specified (can be empty array)");
        if (Routing.Click is null) throw new ArgumentException("Routing.Click must be specified (can be empty array)");
        // OutputBackend/OutputDevice optional for now.
    }
}

public sealed class RoutingOptions
{
    // 1-based channel indices mapping logical buses to physical channels.
    public int[] Tracks { get; set; } = Array.Empty<int>();
    public int[] Click { get; set; } = Array.Empty<int>();
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
