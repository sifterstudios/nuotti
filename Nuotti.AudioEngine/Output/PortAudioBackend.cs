using Nuotti.AudioEngine.Playback;
namespace Nuotti.AudioEngine.Output;

// Stub implementation for future multi-output using PortAudio
public sealed class PortAudioBackend : IAudioBackend
{
    public string Name => "PortAudio";

    public IAudioPlayer CreatePlayer(EngineOptions options)
    {
        // For now, return a Noop player as a stub.
        return new NoopPlayer();
    }
}