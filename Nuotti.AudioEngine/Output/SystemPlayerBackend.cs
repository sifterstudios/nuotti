using Nuotti.AudioEngine.Playback;
namespace Nuotti.AudioEngine.Output;

public sealed class SystemPlayerBackend : IAudioBackend
{
    public string Name => "SystemPlayer";

    public IAudioPlayer CreatePlayer(EngineOptions options)
    {
        var preferred = options?.PreferredPlayer ?? PreferredPlayer.Auto;
        return new SystemPlayer(preferred);
    }
}