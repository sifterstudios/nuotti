using Nuotti.Contracts.V1.Model;
namespace Nuotti.Contracts.V1.Message.Phase;

/// <summary>
/// Opens the next song.
/// Allowed phases: Lobby, Intermission
/// </summary>
public sealed record NextRound(SongId SongId) : CommandBase, IPhaseRestricted
{
    public SongId SongId { get; } = SongId;
    public IReadOnlyCollection<Enum.Phase> AllowedPhases { get; } =
        [Enum.Phase.Lobby, Enum.Phase.Intermission];
}