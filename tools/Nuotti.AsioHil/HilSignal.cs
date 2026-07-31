using System.Diagnostics;
using NAudio.Wave;

namespace Nuotti.AsioHil;

public enum HilSignalMode
{
    StereoBackingAndClick,
    MonoBackingAndClick
}

public sealed class HilSignal
{
    private readonly int _sampleRate;
    private readonly long _durationFrames;
    private readonly long _backingOffsetFrames;

    public HilSignal(int sampleRate, long durationFrames, long backingOffsetFrames, HilSignalMode mode = HilSignalMode.StereoBackingAndClick)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(sampleRate, 1);
        ArgumentOutOfRangeException.ThrowIfLessThan(durationFrames, 1);
        ArgumentOutOfRangeException.ThrowIfNegative(backingOffsetFrames);
        _sampleRate = sampleRate;
        _durationFrames = durationFrames;
        _backingOffsetFrames = backingOffsetFrames;
        Mode = mode;
    }

    public long FramePosition { get; private set; }
    public int SampleRate => _sampleRate;
    public HilSignalMode Mode { get; }
    public int Channels => Mode == HilSignalMode.MonoBackingAndClick ? 2 : 3;

    public int ReadFrames(float[] destination, int requestedFrames)
    {
        var frames = (int)Math.Min(requestedFrames, _durationFrames - FramePosition);
        Array.Clear(destination, 0, frames * Channels);
        for (var localFrame = 0; localFrame < frames; localFrame++)
        {
            var frame = FramePosition + localFrame;
            var index = localFrame * Channels;
            if (frame >= _backingOffsetFrames && (frame - _backingOffsetFrames) % _sampleRate == 0)
            {
                destination[index] = 0.125f;
                if (Mode == HilSignalMode.StereoBackingAndClick)
                {
                    destination[index + 1] = -0.125f;
                }
            }
            if (frame % _sampleRate == 0)
            {
                destination[index + Channels - 1] = 0.125f;
            }
        }
        FramePosition += frames;
        return frames;
    }
}

public sealed class HilWaveProvider : IWaveProvider
{
    private readonly HilSignal _signal;
    private readonly float[] _samples;
    private readonly Stopwatch _clock;

    public HilWaveProvider(HilSignal signal, Stopwatch clock, int maximumFrames = 4096)
    {
        _signal = signal;
        _clock = clock;
        _samples = new float[maximumFrames * signal.Channels];
        WaveFormat = WaveFormat.CreateIeeeFloatWaveFormat(signal.SampleRate, signal.Channels);
    }

    public WaveFormat WaveFormat { get; }
    public TimeSpan? FirstCallbackAt { get; private set; }

    public int Read(byte[] buffer, int offset, int count)
    {
        FirstCallbackAt ??= _clock.Elapsed;
        var requestedFrames = Math.Min(count / WaveFormat.BlockAlign, _samples.Length / _signal.Channels);
        var frames = _signal.ReadFrames(_samples, requestedFrames);
        var bytes = frames * WaveFormat.BlockAlign;
        Buffer.BlockCopy(_samples, 0, buffer, offset, bytes);
        return bytes;
    }
}
