using Nuotti.Contracts.V1.Model;
namespace Nuotti.Contracts.V1.Message.Phase;

/// <summary>
/// Reveals the correct answer choice id.
/// Allowed phases: Lock.
/// </summary>
public sealed record RevealAnswer(SongRef SongRef) : CommandBase, IPhaseRestricted, IPhaseChange
{
    public SongRef SongRef { get; } = SongRef;
    public IReadOnlyCollection<Enum.Phase> AllowedPhases { get; } = [Enum.Phase.Lock];

    public Enum.Phase TargetPhase => Enum.Phase.Reveal;
    public IReadOnlyCollection<Enum.Phase> AllowedSourcePhases => [Enum.Phase.Lock];
    public bool IsPhaseChangeAllowed(Enum.Phase current) => AllowedSourcePhases.Contains(current);
}