using Nuotti.Contracts.V1.Message;
namespace Nuotti.Contracts.V1.Event;

/// <summary>
/// Emitted by Backend when the Performer reveals the next hint for the current song.
/// </summary>
/// <param name="HintIndex">The hint index the session moves to.</param>
public sealed record HintGiven(int HintIndex) : EventBase;
