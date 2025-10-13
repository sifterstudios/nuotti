namespace Nuotti.AudioEngine.Playback.PortAudio;

/// <summary>
/// Simulated PortAudio engine that logs writes and sleeps for the duration to mimic real-time playback.
/// Keeps existing test expectations intact.
/// </summary>
public sealed class SimulatedPortAudioEngine : IPortAudioEngine
{
    private int _sampleRate;
    private int _channels;
    private bool _started;

    public double ReportedLatencyMs => 0;

    public void Open(int sampleRate, int channels)
    {
        _sampleRate = sampleRate > 0 ? sampleRate : 48000;
        _channels = Math.Max(1, channels);
    }

    public void Start()
    {
        _started = true;
    }

    public async Task WriteAsync(float[] buffer, int frames, int channels, int sampleRate, CancellationToken cancellationToken)
    {
        if (!_started) return;
        // Preserve the original log message used by tests
        Console.WriteLine($"[AudioEngine] PortAudio simulated write: {frames} frames @ {sampleRate} Hz to {channels}ch");
        double seconds = (double)frames / Math.Max(1, sampleRate);
        try { await Task.Delay(TimeSpan.FromSeconds(seconds), cancellationToken); } catch { }
    }

    public void Stop()
    {
        _started = false;
    }

    public void Close()
    {
        _started = false;
        _sampleRate = 0;
        _channels = 0;
    }
}