using Nuotti.Contracts.V1.Enum;
namespace Nuotti.Contracts.V1.Message;

/// <summary>
/// Event broadcast when the Audio Engine status changes.
/// </summary>
/// <param name="Status">The new engine status.</param>
public record EngineStatusChanged(EngineStatus Status);
