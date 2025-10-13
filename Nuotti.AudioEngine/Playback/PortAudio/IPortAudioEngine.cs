namespace Nuotti.AudioEngine.Playback.PortAudio;

/// <summary>
/// Abstraction for a PortAudio-backed output engine. Allows swapping between a simulated engine and a real one.
/// </summary>
public interface IPortAudioEngine
{
    /// <summary>Opens/initializes the engine for a specific sample rate and channel count.</summary>
    /// <param name="sampleRate">Sample rate in Hz.</param>
    /// <param name="channels">Number of interleaved output channels.</param>
    void Open(int sampleRate, int channels);

    /// <summary>Starts the output engine/stream.</summary>
    void Start();

    /// <summary>
    /// Writes interleaved float frames to the output.
    /// </summary>
    /// <param name="buffer">Interleaved float samples.</param>
    /// <param name="frames">Number of frames (frame = one sample per channel).</param>
    /// <param name="channels">Number of interleaved channels in the buffer.</param>
    /// <param name="sampleRate">Sample rate in Hz.</param>
    /// <param name="cancellationToken">Cancellation token to abort any blocking wait.</param>
    Task WriteAsync(float[] buffer, int frames, int channels, int sampleRate, CancellationToken cancellationToken);

    /// <summary>Stops the engine/stream.</summary>
    void Stop();

    /// <summary>Closes/releases any resources.</summary>
    void Close();
}