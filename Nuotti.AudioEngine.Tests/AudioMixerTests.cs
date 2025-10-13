using Nuotti.AudioEngine.Playback;
using Xunit;
namespace Nuotti.AudioEngine.Tests;

public class AudioMixerTests
{
    [Fact]
    public void Mix_SumsAndClamps_PositiveOverflow()
    {
        var a = new float[] { 0.6f, 0.9f };
        var b = new float[] { 0.6f, 0.5f };
        var dest = new float[2];
        AudioMixer.Mix(a, b, dest, 2);
        Assert.Equal(1f, dest[0]);
        Assert.Equal(1f, dest[1]);
    }

    [Fact]
    public void Mix_SumsAndClamps_NegativeOverflow()
    {
        var a = new float[] { -0.6f, -0.9f };
        var b = new float[] { -0.6f, -0.5f };
        var dest = new float[2];
        AudioMixer.Mix(a, b, dest, 2);
        Assert.Equal(-1f, dest[0]);
        Assert.Equal(-1f, dest[1]);
    }

    [Fact]
    public void MixInPlace_WorksAndClamps()
    {
        var dest = new float[] { 0.75f, -0.8f, 0.1f };
        var add = new float[] { 0.5f, -0.4f, 0.2f };
        AudioMixer.MixInPlace(dest, add, 3);
        Assert.Equal(1f, dest[0]);
        Assert.Equal(-1f, dest[1]);
        Assert.InRange(dest[2], 0.3f - 1e-6f, 0.3f + 1e-6f);
    }
}
