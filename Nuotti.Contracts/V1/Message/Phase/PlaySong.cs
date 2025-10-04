using Nuotti.Contracts.V1.Model;
namespace Nuotti.Contracts.V1.Message.Phase;

/// <summary>
/// Starts playing a track for the current song.
/// Allowed phases: Play
/// </summary>
public sealed record PlaySong(SongId SongId) : CommandBase, IPhaseRestricted, IPhaseChange
{
    public SongId SongId { get; } = SongId;
    public IReadOnlyCollection<Enum.Phase> AllowedPhases { get; } = [Enum.Phase.Play];

    public Enum.Phase TargetPhase => Enum.Phase.Play;
    public IReadOnlyCollection<Enum.Phase> AllowedSourcePhases => [Enum.Phase.Reveal];
    public bool IsPhaseChangeAllowed(Enum.Phase current) => AllowedSourcePhases.Contains(current);
}