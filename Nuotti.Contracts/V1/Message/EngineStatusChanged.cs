using Nuotti.Contracts.V1.Enum;
namespace Nuotti.Contracts.V1.Message;

/// <summary>
/// Event broadcast when the Audio Engine status changes.
/// </summary>
/// <param name="Status">The new engine status.</param>
/// <param name="LatencyMs">Measured output latency in milliseconds (device buffer + reported stream latency).</param>
public record EngineStatusChanged(EngineStatus Status, double LatencyMs);
