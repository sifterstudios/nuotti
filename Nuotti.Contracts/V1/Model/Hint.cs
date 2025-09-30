namespace Nuotti.Contracts.V1.Model;

/// <summary>
/// A hint shown or played to/for participants, ordered by index.
/// </summary>
/// <param name="Index">Zero-based ordering of the hint relative to other hints for the question</param>
/// <param name="Text">Optional hint text shown to participants</param>
/// <param name="PerformerInstructions">Optional instructions for performer</param>
public sealed record Hint(int Index, string? Text, string? PerformerInstructions);