using Nuotti.Contracts.V1.Message;
namespace Nuotti.Contracts.V1.Event;

/// <summary>
/// A Guessing Window has opened. Carries the clamped duration; opened-at is <see cref="EventBase.EmittedAtUtc"/>.
/// </summary>
/// <remarks>
/// Emitted alongside <see cref="GamePhaseChanged"/> when the Performer issues OpenAnswers.
/// The Reducer owns the snapshot clock fields and clears live answers for the new Window;
/// Lock-held answers remain for Reveal.
/// </remarks>
/// <param name="WindowSeconds">Duration of the Window in seconds (already clamped 10–120).</param>
public sealed record GuessingWindowOpened(int WindowSeconds) : EventBase
{
    public required int WindowSeconds { get; init; } = WindowSeconds;
}
