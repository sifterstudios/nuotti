namespace Nuotti.Contracts.V1.Model;

/// <summary>
/// A single selectable choice in a multiple-choice question
/// </summary>
/// <param name="Id">Stable choice identifier (unique within a question)</param>
/// <param name="Text">Display text of the choice.</param>
public sealed record Choice(string Id, string Text);