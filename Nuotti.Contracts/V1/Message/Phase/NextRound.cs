using Nuotti.Contracts.V1.Model;
namespace Nuotti.Contracts.V1.Message.Phase;

/// <summary>
/// Opens the next song.
/// Allowed phases: Intermission (the normal path, between rounds), Guessing (abandon the current
/// round and skip straight to the next song).
/// </summary>
/// <remarks>
/// Intermission was added because it is where a round actually ends - EndSong moves the session
/// there - and previously nothing could leave it, so the round loop had no way back to Start.
/// </remarks>
public sealed record NextRound(SongId SongId) : CommandBase, IPhaseRestricted, IPhaseChange
{
    public SongId SongId { get; } = SongId;
    public IReadOnlyCollection<Enum.Phase> AllowedPhases { get; } =
        [Enum.Phase.Intermission, Enum.Phase.Guessing];

    public Enum.Phase TargetPhase => Enum.Phase.Start;
    public IReadOnlyCollection<Enum.Phase> AllowedSourcePhases =>
        [Enum.Phase.Intermission, Enum.Phase.Guessing];
    public bool IsPhaseChangeAllowed(Enum.Phase current) => AllowedSourcePhases.Contains(current);
}
