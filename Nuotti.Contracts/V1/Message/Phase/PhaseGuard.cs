using PhaseEnum = Nuotti.Contracts.V1.Enum.Phase;

namespace Nuotti.Contracts.V1.Message.Phase;

/// <summary>
/// Helpers for validating phase restrictions on commands.
/// </summary>
public static class PhaseGuard
{
    /// <summary>
    /// Throws <see cref="PhaseViolationException"/> if the given command is <see cref="IPhaseRestricted"/>
    /// and the provided <paramref name="current"/> phase is not within the command's <c>AllowedPhases</c>.
    /// Non-restricted commands pass through without checks.
    /// </summary>
    public static void EnsureAllowed(PhaseEnum current, object command)
    {
        if (command is IPhaseRestricted restricted)
        {
            IReadOnlyCollection<PhaseEnum> allowed = (IReadOnlyCollection<PhaseEnum>)restricted.AllowedPhases;
            if (!allowed.Contains(current))
            {
                throw new PhaseViolationException(current, command.GetType(), allowed);
            }
        }
    }

    /// <summary>
    /// If the command implements <see cref="IPhaseChange"/>, validates that the transition from
    /// <paramref name="current"/> to the command's <c>TargetPhase</c> is allowed via
    /// <see cref="IPhaseChange.IsPhaseChangeAllowed(PhaseEnum)"/>.
    /// </summary>
    public static void EnsureChangeAllowed(PhaseEnum current, object command)
    {
        if (command is IPhaseChange changer)
        {
            if (!changer.IsPhaseChangeAllowed(current))
            {
                // Reuse PhaseViolationException, passing the allowed source phases for diagnostic info
                throw new PhaseViolationException(current, command.GetType(), changer.AllowedSourcePhases);
            }
        }
    }
}
