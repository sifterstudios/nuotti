namespace Nuotti.Contracts.V1.Protocol;

/// <summary>Identifies one playback attempt under the controller generation that authorized it.</summary>
public sealed record PlaybackIdentity(string PlaybackInstanceId, ControlGeneration ControlGeneration);
