using Nuotti.Contracts.V1.Model;
namespace Nuotti.Contracts.V1.Message.Phase;

/// <summary>
/// Starts playing a track for the current song.
/// Allowed phases: Reveal
/// </summary>
public sealed record PlaySong(SongId SongId) : CommandBase, IPhaseRestricted
{
    public SongId SongId { get; } = SongId;
    public IReadOnlyCollection<Enum.Phase> AllowedPhases { get; } = [Enum.Phase.Reveal];
}