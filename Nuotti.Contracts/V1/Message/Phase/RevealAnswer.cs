using Nuotti.Contracts.V1.Model;
namespace Nuotti.Contracts.V1.Message.Phase;

/// <summary>
/// Reveals the correct answer choice id.
/// Allowed phases: Lock.
/// </summary>
public sealed record RevealAnswer(SongRef SongRef) : CommandBase, IPhaseRestricted
{
    public SongRef SongRef { get; } = SongRef;
    public IReadOnlyCollection<Enum.Phase> AllowedPhases { get; } = [Enum.Phase.Lock];
}