using Nuotti.AudioEngine.Playback;
namespace Nuotti.AudioEngine.Output;

public interface IAudioBackend
{
    string Name { get; }
    IAudioPlayer CreatePlayer(EngineOptions options);
}