using Nuotti.Contracts.V1.Model;
namespace Nuotti.Contracts.V1.Message.Phase;

/// <summary>
/// Starts a game.
/// Allowed phases: Lobby.
/// </summary>
public sealed record StartGame : CommandBase, IPhaseRestricted
{
    public IReadOnlyCollection<Enum.Phase> AllowedPhases { get; } =
        [Enum.Phase.Lobby];
}
/// <summary>
/// Opens the next song.
/// Allowed phases: Lobby, Intermission
/// </summary>
public sealed record NextRound(SongId SongId) : CommandBase, IPhaseRestricted
{
    public IReadOnlyCollection<Enum.Phase> AllowedPhases { get; } =
        [Enum.Phase.Lobby, Enum.Phase.Intermission];
}
/// <summary>
/// Increments hint counter.
/// Allowed phases: SongOpen.
/// </summary>
public sealed record GiveHint(Hint Hint) : CommandBase, IPhaseRestricted
{
    public IReadOnlyCollection<Enum.Phase> AllowedPhases { get; } =
        [Enum.Phase.Start, Enum.Phase.Hint];
}
/// <summary>
/// Locks audience answers.
/// Allowed phases: SongOpen.
/// </summary>
public sealed record LockAnswers() : CommandBase, IPhaseRestricted
{
    public IReadOnlyCollection<Enum.Phase> AllowedPhases { get; } = [Enum.Phase.Guessing];
}
/// <summary>
/// Reveals the correct answer choice id.
/// Allowed phases: AnswersLocked.
/// </summary>
public sealed record RevealAnswer(SongRef SongRef) : CommandBase, IPhaseRestricted
{
    public IReadOnlyCollection<Enum.Phase> AllowedPhases { get; } = [Enum.Phase.Lock];
}
/// <summary>
/// Starts playing a track for the current song.
/// Allowed phases: SongOpen, AnswersLocked (configurable).
/// </summary>
public sealed record PlaySong(SongId SongId) : CommandBase, IPhaseRestricted
{
    public IReadOnlyCollection<Enum.Phase> AllowedPhases { get; } = [Enum.Phase.Reveal];
}
/// <summary>
/// Ends the current song.
/// Allowed phases: AnswerRevealed.
/// </summary>
public sealed record EndSong(SongId SongId): CommandBase;
/// <summary>
/// Audience submits or updates an answer.
/// Allowed phases: Guessing.
/// </summary>
public sealed record SubmitAnswer(SongId SongId) : CommandBase, IPhaseRestricted
{
    public IReadOnlyCollection<Enum.Phase> AllowedPhases { get; } = [Enum.Phase.Guessing];
}