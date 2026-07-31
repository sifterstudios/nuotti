using System.Text.Json.Serialization;
using Nuotti.Contracts.V1.Protocol;

namespace Nuotti.Contracts.V1.Message;

/// <summary>Stops the currently playing track.</summary>
public record StopTrack() : CommandBase
{
    /// <summary>The playback attempt this relay targets. Null preserves the legacy relay contract.</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? PlaybackInstanceId { get; init; }

    /// <summary>The Backend control generation authorizing this relay. Null preserves legacy behavior.</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public ControlGeneration? ControlGeneration { get; init; }
}
