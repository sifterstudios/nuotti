using Nuotti.AsioHil;
using Xunit;

namespace Nuotti.AudioEngine.Tests;

public class AsioHilSignalTests
{
    [Fact]
    public void Signal_uses_one_frame_counter_for_backing_and_click()
    {
        var signal = new HilSignal(sampleRate: 48_000, durationFrames: 96_000, backingOffsetFrames: 48_000);
        var samples = new float[3 * 48_001];

        var frames = signal.ReadFrames(samples, 48_001);

        Assert.Equal(48_001, frames);
        Assert.Equal(0f, samples[0]);
        Assert.Equal(0f, samples[1]);
        Assert.Equal(0.125f, samples[2]);
        Assert.Equal(0.125f, samples[48_000 * 3]);
        Assert.Equal(-0.125f, samples[48_000 * 3 + 1]);
        Assert.Equal(0.125f, samples[48_000 * 3 + 2]);
        Assert.Equal(48_001, signal.FramePosition);
    }

    [Fact]
    public void Mono_mode_routes_backing_and_click_to_two_outputs_on_the_same_timeline()
    {
        var signal = new HilSignal(48_000, 96_000, 48_000, HilSignalMode.MonoBackingAndClick);
        var samples = new float[2 * 48_001];

        signal.ReadFrames(samples, 48_001);

        Assert.Equal(2, signal.Channels);
        Assert.Equal(0f, samples[0]);
        Assert.Equal(0.125f, samples[1]);
        Assert.Equal(0.125f, samples[48_000 * 2]);
        Assert.Equal(0.125f, samples[48_000 * 2 + 1]);
    }
}
