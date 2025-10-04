using System.Collections.Generic;
using PhaseEnum = Nuotti.Contracts.V1.Enum.Phase;

namespace Nuotti.Contracts.V1.Message.Phase;

/// <summary>
/// Composable marker for commands that initiate a phase transition.
/// Implementers should declare the <see cref="TargetPhase"/> they move the session into
/// and the allowed source phases from which that transition may occur.
/// </summary>
public interface IPhaseChange
{
    /// <summary>
    /// The phase this command intends to transition the session into.
    /// </summary>
    PhaseEnum TargetPhase { get; }

    /// <summary>
    /// The set of source phases from which transitioning to <see cref="TargetPhase"/> is allowed.
    /// </summary>
    IReadOnlyCollection<PhaseEnum> AllowedSourcePhases { get; }

    /// <summary>
    /// Returns true if transitioning from <paramref name="current"/> to <see cref="TargetPhase"/> is allowed.
    /// </summary>
    bool IsPhaseChangeAllowed(PhaseEnum current);
}