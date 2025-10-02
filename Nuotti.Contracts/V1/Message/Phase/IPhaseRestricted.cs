namespace Nuotti.Contracts.V1.Message.Phase;

/// <summary>
/// Marker interface for commands that can only be executed in specific session phases.
/// A command handler should validate the current session phase against <see cref="AllowedPhases"/>
/// before applying the command and emitting events.
/// </summary>
public interface IPhaseRestricted
{
    /// <summary>
    /// The set of phases in which this command is permitted to execute.
    /// </summary>
    IReadOnlyCollection<Enum.Phase> AllowedPhases { get; }
}