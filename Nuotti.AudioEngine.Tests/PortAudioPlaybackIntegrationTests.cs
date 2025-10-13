using Microsoft.Extensions.Options;
using Nuotti.AudioEngine.Output;
using Nuotti.AudioEngine.Playback;
using System;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using Xunit;
namespace Nuotti.AudioEngine.Tests;

public class PortAudioPlaybackIntegrationTests
{
    private static string CreateTestWav(int seconds = 1, int sampleRate = 8000, int channels = 1)
    {
        // Generate a simple sine wave WAV PCM16
        var tmp = Path.Combine(Path.GetTempPath(), $"nuotti_test_{Guid.NewGuid():N}.wav");
        int frames = seconds * sampleRate;
        short[] samples = new short[frames * channels];
        double freq = 440.0;
        for (int i = 0; i < frames; i++)
        {
            short s = (short)(Math.Sin(2 * Math.PI * freq * i / sampleRate) * short.MaxValue * 0.1);
            for (int ch = 0; ch < channels; ch++)
                samples[i * channels + ch] = s;
        }
        using var fs = File.Create(tmp);
        using var bw = new BinaryWriter(fs);
        int byteRate = sampleRate * channels * 2;
        int blockAlign = channels * 2;
        int dataBytes = samples.Length * 2;
        // RIFF header
        bw.Write(Encoding.ASCII.GetBytes("RIFF"));
        bw.Write(36 + dataBytes);
        bw.Write(Encoding.ASCII.GetBytes("WAVE"));
        // fmt chunk
        bw.Write(Encoding.ASCII.GetBytes("fmt "));
        bw.Write(16);
        bw.Write((short)1); // PCM
        bw.Write((short)channels);
        bw.Write(sampleRate);
        bw.Write(byteRate);
        bw.Write((short)blockAlign);
        bw.Write((short)16);
        // data chunk
        bw.Write(Encoding.ASCII.GetBytes("data"));
        bw.Write(dataBytes);
        foreach (var s in samples) bw.Write(s);
        bw.Flush();
        return tmp;
    }

    [Fact]
    public async Task Simulated_PortAudio_Writes_Frames_No_Exception()
    {
        var opts = new EngineOptions
        {
            OutputBackend = "PortAudio",
            Routing = new RoutingOptions { Tracks = new[] { 1, 2 } }
        };
        IOptions<EngineOptions> iopts = Options.Create(opts);
        var backend = new PortAudioBackend();
        IAudioPlayer player = backend.CreatePlayer(iopts.Value);

        // Capture console
        using var sw = new StringWriter();
        var originalOut = Console.Out;
        Console.SetOut(sw);

        // Play temp wav
        var wav = CreateTestWav(seconds: 1, sampleRate: 8000, channels: 1);
        try
        {
            Exception? error = null;
            player.Error += (_, ex) => error = ex;
            await player.PlayAsync(wav);
            await Task.Delay(300); // allow it to start and process some buffers
            await player.StopAsync();
            await Task.Delay(100);
            Assert.Null(error);
            var log = sw.ToString();
            Assert.Contains("PortAudio simulated write", log);
        }
        finally
        {
            Console.SetOut(originalOut);
            try { File.Delete(wav); } catch { }
        }
    }
}
