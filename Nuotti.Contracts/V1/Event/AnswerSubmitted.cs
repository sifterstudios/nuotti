using Nuotti.Contracts.V1.Enum;
using Nuotti.Contracts.V1.Message;
namespace Nuotti.Contracts.V1.Event;

/// <summary>
/// Sent by Audience to Backend when an answer is submitted.
/// </summary>
/// <param name="AudienceId">Identifier of the audience member (may mirror ConnectionId).</param>
/// <param name="ChoiceIndex">Index into the question's Options array.</param>
public sealed record AnswerSubmitted(string AudienceId, int ChoiceIndex) : EventBase
{
    public required string AudienceId { get; init; } = AudienceId;
    public required int ChoiceIndex { get; init; } = ChoiceIndex;
}

public sealed record GamePhaseChanged(Phase CurrentPhase, Phase NewPhase) : EventBase
{
    public required Phase CurrentPhase { get; init; } = CurrentPhase;
    public required Phase NewPhase { get; init; } = NewPhase;
}