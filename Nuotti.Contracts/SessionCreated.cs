namespace Nuotti.Contracts.V1;

/// <summary>
/// Emitted by Backend when a new session (room) is created and ready.
/// </summary>
/// <param name="SessionCode">Human-friendly join code displayed on the Projector.</param>
/// <param name="HostId">Identifier for the host (e.g., Backend connection or instance id).</param>
public readonly record struct SessionCreated(string SessionCode, string HostId);
