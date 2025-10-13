namespace Nuotti.AudioEngine.Playback.Routing;

public interface IChannelRouter
{
    // Map input interleaved frames to device channels according to engine routing.
    // inFrames: number of frames in the source buffer; inChannels: source channel count; outChannels: device channel count.
    // src: length >= inFrames * inChannels; dst: length >= inFrames * outChannels
    void Route(float[] src, int inFrames, int inChannels, float[] dst, int outChannels);
}
