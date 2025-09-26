namespace Nuotti.Contracts;

/// <summary>
/// Command to the AudioEngine to play a single audio track from a URL (MVP).
/// </summary>
/// <param name="FileUrl">Public or accessible URL to the audio file to play.</param>
public readonly record struct PlayTrack(string FileUrl);
