namespace Nuotti.AudioEngine.Playback;

public static class AudioMixer
{
    // Mixes two buffers into dest with clamping to [-1, 1]. Length is number of samples (floats), not frames.
    public static void Mix(float[] a, float[] b, float[] dest, int length)
    {
        if (a == null) throw new ArgumentNullException(nameof(a));
        if (b == null) throw new ArgumentNullException(nameof(b));
        if (dest == null) throw new ArgumentNullException(nameof(dest));
        if (length < 0) throw new ArgumentOutOfRangeException(nameof(length));
        if (a.Length < length || b.Length < length || dest.Length < length)
            throw new ArgumentException("Input or destination buffer too small for length");

        for (int i = 0; i < length; i++)
        {
            var s = a[i] + b[i];
            dest[i] = ClampSample(s);
        }
    }

    // Adds b into dest in-place with clamping to [-1, 1]. Length is number of samples (floats), not frames.
    public static void MixInPlace(float[] dest, float[] b, int length)
    {
        if (dest == null) throw new ArgumentNullException(nameof(dest));
        if (b == null) throw new ArgumentNullException(nameof(b));
        if (length < 0) throw new ArgumentOutOfRangeException(nameof(length));
        if (dest.Length < length || b.Length < length)
            throw new ArgumentException("Input or destination buffer too small for length");

        for (int i = 0; i < length; i++)
        {
            var s = dest[i] + b[i];
            dest[i] = ClampSample(s);
        }
    }

    private static float ClampSample(float v)
    {
        if (v > 1f) return 1f;
        if (v < -1f) return -1f;
        return v;
    }
}
