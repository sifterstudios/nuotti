namespace Nuotti.Contracts.V1.Message.Phase;

/// <summary>
/// Locks audience answers.
/// Allowed phases: SongOpen.
/// </summary>
public sealed record LockAnswers : CommandBase, IPhaseRestricted
{
    public IReadOnlyCollection<Enum.Phase> AllowedPhases { get; } = [Enum.Phase.Guessing];
}