namespace Nuotti.AudioEngine.Playback;

/// <summary>
/// Metronome/click source lifecycle tied to track playback.
/// Stubbed interface for initial implementation.
/// </summary>
public interface IClickSource
{
    /// <summary>
    /// Whether the click is enabled based on configuration (e.g., Level > 0) and routing availability.
    /// </summary>
    bool Enabled { get; }

    /// <summary>
    /// Start click generation (no-op if Disabled).
    /// </summary>
    void Start();

    /// <summary>
    /// Stop click generation.
    /// </summary>
    void Stop();
}
