using Nuotti.AudioEngine.Playback;
using Nuotti.AudioEngine.Playback.Decoding;
using Nuotti.AudioEngine.Playback.PortAudio;
using Nuotti.AudioEngine.Playback.Routing;
namespace Nuotti.AudioEngine.Output;

// Backend that wires up a PortAudio-based player. Currently uses simulated engine; real engine can be plugged later.
public sealed class PortAudioBackend : IAudioBackend
{
    public string Name => "PortAudio";

    public IAudioPlayer CreatePlayer(EngineOptions options)
    {
        var decoder = new WavPcm16Decoder();
        var router = new SimpleChannelRouter(options.Routing?.Tracks ?? Array.Empty<int>());
        // Choose real PortAudio engine if explicitly enabled, otherwise keep simulated for tests and environments without native deps.
        IPortAudioEngine engine = options.UsePortAudioSharp2
            ? new RealPortAudioEngine()
            : new SimulatedPortAudioEngine();
        return new PortAudioPlayer(decoder, router, options, engine);
    }
}