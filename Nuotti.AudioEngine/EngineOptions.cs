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
    // If true, the PortAudio backend will use the PortAudioSharp2 package for real audio output instead of the simulated engine.
    public bool UsePortAudioSharp2 { get; set; } = false;

    // Optional safety options for sources (path allowlist, HTTP size limit)
    public AudioEngineSafetyOptions Safety { get; set; } = new();

    // Optional metrics exposure (/metrics via HTTP or console dump on signal)
    public MetricsOptions Metrics { get; set; } = new();

    public void Validate()
    {
        var errors = new List<string>();

        // Ensure Routing present and arrays initialized (can be empty)
        if (Routing is null)
        {
            errors.Add("Routing section is required. Hint: Add 'Routing' section to engine.json or set NUOTTI_ENGINE__ROUTING__TRACKS and NUOTTI_ENGINE__ROUTING__CLICK environment variables");
        }
        else
        {
            if (Routing.Tracks is null)
            {
                errors.Add("Routing.Tracks must be specified (can be empty array). Hint: Set NUOTTI_ENGINE__ROUTING__TRACKS=[] or add to engine.json");
            }
            if (Routing.Click is null)
            {
                errors.Add("Routing.Click must be specified (can be empty array). Hint: Set NUOTTI_ENGINE__ROUTING__CLICK=[] or add to engine.json");
            }
        }

        // Validate click options
        if (Click is null)
        {
            errors.Add("Click section is required. Hint: Add 'Click' section to engine.json or set NUOTTI_ENGINE__CLICK__LEVEL and NUOTTI_ENGINE__CLICK__BPM environment variables");
        }
        else
        {
            if (Click.Level is < 0 or > 1)
            {
                errors.Add($"Click.Level must be between 0 and 1 inclusive (current: {Click.Level}). Hint: Set NUOTTI_ENGINE__CLICK__LEVEL environment variable or add to engine.json");
            }
            if (Click.Bpm <= 0)
            {
                errors.Add($"Click.Bpm must be positive (current: {Click.Bpm}). Hint: Set NUOTTI_ENGINE__CLICK__BPM environment variable or add to engine.json");
            }
        }

        // OutputBackend/OutputDevice optional for now.

        if (errors.Count > 0)
        {
            var errorMessage = string.Join("; ", errors);
            throw new ArgumentException($"Invalid EngineOptions configuration: {errorMessage}");
        }
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
