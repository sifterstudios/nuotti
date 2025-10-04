using Nuotti.Contracts.V1.Model;
namespace Nuotti.Contracts.V1.Message.Phase;

/// <summary>
/// Opens the next song.
/// Allowed phases: Guessing
/// </summary>
public sealed record NextRound(SongId SongId) : CommandBase, IPhaseRestricted, IPhaseChange
{
    public SongId SongId { get; } = SongId;
    public IReadOnlyCollection<Enum.Phase> AllowedPhases { get; } =
        [Enum.Phase.Guessing];

    public Enum.Phase TargetPhase => Enum.Phase.Start;
    public IReadOnlyCollection<Enum.Phase> AllowedSourcePhases => [Enum.Phase.Guessing];
    public bool IsPhaseChangeAllowed(Enum.Phase current) => AllowedSourcePhases.Contains(current);
}