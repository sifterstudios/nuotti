namespace Nuotti.AudioEngine.Playback.Routing;

public sealed class SimpleChannelRouter : IChannelRouter
{
    private readonly int[] _tracks; // 1-based output channel indices

    public SimpleChannelRouter(int[] tracks)
    {
        _tracks = tracks ?? Array.Empty<int>();
    }

    public void Route(float[] src, int inFrames, int inChannels, float[] dst, int outChannels)
    {
        Array.Clear(dst, 0, inFrames * outChannels);
        if (inFrames <= 0 || inChannels <= 0 || outChannels <= 0) return;

        // If no routing specified, map first inChannels to first outChannels 1:1
        if (_tracks.Length == 0)
        {
            int map = Math.Min(inChannels, outChannels);
            for (int f = 0; f < inFrames; f++)
            {
                int sBase = f * inChannels;
                int dBase = f * outChannels;
                for (int ch = 0; ch < map; ch++)
                {
                    dst[dBase + ch] = src[sBase + ch];
                }
            }
            return;
        }

        // For each configured physical channel index, route a source channel to it.
        for (int i = 0; i < _tracks.Length; i++)
        {
            int physCh1Based = _tracks[i];
            if (physCh1Based <= 0) continue;
            int physCh = physCh1Based - 1; // 0-based index into dst
            if (physCh >= outChannels) continue;
            int srcCh = i % inChannels; // cycle through source channels
            for (int f = 0; f < inFrames; f++)
            {
                int sIndex = f * inChannels + srcCh;
                int dIndex = f * outChannels + physCh;
                dst[dIndex] = src[sIndex];
            }
        }
    }
}
