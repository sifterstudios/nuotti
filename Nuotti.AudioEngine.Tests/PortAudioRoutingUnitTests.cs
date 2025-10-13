using Nuotti.AudioEngine.Playback;
using Nuotti.AudioEngine.Playback.Decoding;
using Nuotti.AudioEngine.Playback.Routing;
using System;
using System.Threading.Tasks;
using Xunit;
namespace Nuotti.AudioEngine.Tests;

public class PortAudioRoutingUnitTests
{
    private sealed class FakeDecoder : IAudioDecoder
    {
        private int _framesRemaining;
        private readonly int _channels;
        public int SampleRate { get; private set; } = 8000;
        public int Channels => _channels;
        public FakeDecoder(int frames, int channels)
        {
            _framesRemaining = frames;
            _channels = channels;
        }
        public void Open(string filePath) { }
        public int Read(float[] buffer, int framesToRead)
        {
            int frames = Math.Min(framesToRead, _framesRemaining);
            if (frames <= 0) return 0;
            // Fill with ascending values per channel for easy verification
            for (int f = 0; f < frames; f++)
            {
                for (int ch = 0; ch < _channels; ch++)
                {
                    buffer[f * _channels + ch] = ch + 1; // channel 0 -> 1.0, channel 1 -> 2.0, etc.
                }
            }
            _framesRemaining -= frames;
            return frames;
        }
        public void Close() { }
    }

    private sealed class SpyRouter : IChannelRouter
    {
        public int Calls { get; private set; }
        public int LastInFrames { get; private set; }
        public int LastInChannels { get; private set; }
        public int LastOutChannels { get; private set; }
        public float[]? LastDst { get; private set; }
        public void Route(float[] src, int inFrames, int inChannels, float[] dst, int outChannels)
        {
            Calls++;
            LastInFrames = inFrames;
            LastInChannels = inChannels;
            LastOutChannels = outChannels;
            LastDst = new float[dst.Length];
            Array.Copy(dst, LastDst, dst.Length);
            // Perform a simple routing as production code would, to validate mapping
            // Here simply copy src ch0 to dst ch0, src ch1 to dst ch1 (if present)
            int map = Math.Min(inChannels, outChannels);
            for (int f = 0; f < inFrames; f++)
            {
                for (int ch = 0; ch < map; ch++)
                {
                    dst[f * outChannels + ch] = src[f * inChannels + ch];
                }
            }
        }
    }

    [Fact]
    public async Task Router_Is_Called_With_Configured_Output_Channels()
    {
        var decoder = new FakeDecoder(frames: 256, channels: 2);
        var router = new SpyRouter();
        var options = new EngineOptions { Routing = new RoutingOptions { Tracks = new[] { 1, 2, 3, 4 } } };
        var player = new PortAudioPlayer(decoder, router, options);

        Exception? err = null;
        player.Error += (_, ex) => err = ex;
        await player.PlayAsync("fake://test");
        // Allow processing loop to run
        await Task.Delay(100);
        await player.StopAsync();
        await Task.Delay(50);

        Assert.Null(err);
        Assert.True(router.Calls > 0);
        Assert.Equal(2, router.LastInChannels);
        // Output channels are derived from Tracks max or 2
        Assert.Equal(4, router.LastOutChannels);
    }
}
