namespace Nuotti.AudioEngine;

public sealed class AudioEngineSafetyOptions
{
    /// <summary>
    /// Optional list of allowed root directories for local file playback. If set, any file:// URL
    /// must resolve to a full path that is located under one of these roots.
    /// </summary>
    public string[]? AllowedRoots { get; init; }

    /// <summary>
    /// Optional maximum HTTP content size in megabytes. When set, and when the server provides a known
    /// Content-Length for HEAD, sources whose size exceeds this limit are rejected. If size is unknown,
    /// the guard is skipped.
    /// </summary>
    public int? MaxHttpSizeMB { get; init; }
}
