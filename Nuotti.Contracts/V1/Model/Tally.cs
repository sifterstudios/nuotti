namespace Nuotti.Contracts.V1.Model;

/// <summary>
/// Aggregated vote/answer count for a specific choice.
/// </summary>
/// <param name="ChoiceId">Identifier of the choice the count refers to.</param>
/// <param name="Count">Number of votes/answers recorded for the choice.</param>
public sealed record Tally(string ChoiceId, int Count);