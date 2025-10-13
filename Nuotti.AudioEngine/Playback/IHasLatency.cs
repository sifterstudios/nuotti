namespace Nuotti.AudioEngine.Playback;

/// <summary>
/// Optional interface for players that can report a current output latency measurement.
/// </summary>
public interface IHasLatency
{
    /// <summary>
    /// Estimated/measured output latency in milliseconds.
    /// Should include device buffer duration and any additional stream latency.
    /// </summary>
    double OutputLatencyMs { get; }
}
