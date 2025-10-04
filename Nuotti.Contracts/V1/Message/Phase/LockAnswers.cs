namespace Nuotti.Contracts.V1.Message.Phase;

/// <summary>
/// Locks audience answers.
/// Allowed phases: SongOpen.
/// </summary>
public sealed record LockAnswers : CommandBase, IPhaseRestricted, IPhaseChange
{
    public IReadOnlyCollection<Enum.Phase> AllowedPhases { get; } = [Enum.Phase.Guessing];

    public Enum.Phase TargetPhase => Enum.Phase.Lock;
    public IReadOnlyCollection<Enum.Phase> AllowedSourcePhases => [Enum.Phase.Guessing];
    public bool IsPhaseChangeAllowed(Enum.Phase current) => AllowedSourcePhases.Contains(current);
}