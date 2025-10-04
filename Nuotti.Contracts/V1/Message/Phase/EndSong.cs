using Nuotti.Contracts.V1.Model;
namespace Nuotti.Contracts.V1.Message.Phase;

/// <summary>
/// Ends the current song.
/// Allowed phases: Play.
/// </summary>
public sealed record EndSong(SongId SongId) : CommandBase, IPhaseRestricted, IPhaseChange
{
    public SongId SongId { get; } = SongId;
    public IReadOnlyCollection<Enum.Phase> AllowedPhases { get; } = [Enum.Phase.Play];

    public Enum.Phase TargetPhase => Enum.Phase.Intermission;
    public IReadOnlyCollection<Enum.Phase> AllowedSourcePhases => [Enum.Phase.Play];
    public bool IsPhaseChangeAllowed(Enum.Phase current) => AllowedSourcePhases.Contains(current);
}