namespace Nuotti.Contracts.V1.Message;

/// <summary>
/// Command to the AudioEngine to play a single audio track from a URL (MVP).
/// </summary>
/// <param name="FileUrl">Public or accessible URL to the audio file to play.</param>
/// <inheritdoc cref="CommandBase"/>
public record PlayTrack(string FileUrl) : CommandBase;