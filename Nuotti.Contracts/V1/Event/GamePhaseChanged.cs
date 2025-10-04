using Nuotti.Contracts.V1.Enum;
using Nuotti.Contracts.V1.Message;
namespace Nuotti.Contracts.V1.Event;

public sealed record GamePhaseChanged(Phase CurrentPhase, Phase NewPhase) : EventBase
{
    public required Phase CurrentPhase { get; init; } = CurrentPhase;
    public required Phase NewPhase { get; init; } = NewPhase;
}