using Nuotti.Contracts.V1.Message.Phase;

namespace Nuotti.Contracts.V1.Message;

/// <summary>
/// Performer asks the paired Show Agent to verify the Session Setlist Snapshot cache
/// (backing/click) before Start. Relay — no phase change.
/// Allowed phases: Reveal.
/// </summary>
public sealed record PreparePlayback : CommandBase, IPhaseRestricted
{
    public IReadOnlyCollection<Enum.Phase> AllowedPhases { get; } = [Enum.Phase.Reveal];
}
