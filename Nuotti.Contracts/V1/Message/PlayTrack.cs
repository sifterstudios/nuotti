using System.Text.Json.Serialization;
using Nuotti.Contracts.V1.Protocol;

namespace Nuotti.Contracts.V1.Message;

/// <summary>Command to the AudioEngine to play a single audio track from a URL (MVP).</summary>
/// <param name="FileUrl">Public or accessible URL to the audio file to play.</param>
public record PlayTrack(string FileUrl) : CommandBase
{
    /// <summary>The playback attempt this relay targets. Null preserves the legacy relay contract.</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? PlaybackInstanceId { get; init; }

    /// <summary>The Backend control generation authorizing this relay. Null preserves legacy behavior.</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public ControlGeneration? ControlGeneration { get; init; }
}
