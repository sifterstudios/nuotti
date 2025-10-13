using System.Text.Json.Serialization;
namespace Nuotti.AudioEngine;

public sealed class EngineOptions
{
    public PreferredPlayer PreferredPlayer { get; set; } = PreferredPlayer.Auto;
    public string? OutputBackend { get; set; }
    public string? OutputDevice { get; set; }
    public RoutesOptions Routes { get; set; } = new();
    public LogLevel LogLevel { get; set; } = LogLevel.Information;

    public void Validate()
    {
        // Enums are validated by type; ensure Routes present and fields non-empty.
        if (Routes is null) throw new ArgumentException("Routes section is required");
        if (string.IsNullOrWhiteSpace(Routes.Tracks)) throw new ArgumentException("Routes.Tracks is required");
        if (string.IsNullOrWhiteSpace(Routes.Click)) throw new ArgumentException("Routes.Click is required");
        // OutputBackend/OutputDevice optional for now.
    }
}

public sealed class RoutesOptions
{
    public string Tracks { get; set; } = "tracks"; // default logical route name
    public string Click { get; set; } = "click";   // default logical route name
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
