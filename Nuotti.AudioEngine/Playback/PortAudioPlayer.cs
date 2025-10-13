using Nuotti.AudioEngine.Playback.Decoding;
using Nuotti.AudioEngine.Playback.PortAudio;
using Nuotti.AudioEngine.Playback.Routing;
namespace Nuotti.AudioEngine.Playback;

// Minimal in-process player that uses a pluggable PortAudio engine (simulated or real).
public sealed class PortAudioPlayer : IAudioPlayer, IDisposable
{
    private readonly IAudioDecoder _decoder;
    private readonly IChannelRouter _router;
    private readonly EngineOptions _options;
    private readonly int _deviceChannels;
    private readonly IPortAudioEngine _pa;

    private readonly object _gate = new();
    private CancellationTokenSource? _playCts;
    private Task? _playTask;
    private bool _disposed;

    public event EventHandler? Started;
    public event EventHandler<bool>? Stopped; // bool = cancelled
    public event EventHandler<Exception>? Error;

    public bool IsPlaying { get; private set; }

    // Back-compat: old ctor without engine creates a simulated engine
    public PortAudioPlayer(IAudioDecoder decoder, IChannelRouter router, EngineOptions options)
        : this(decoder, router, options, new SimulatedPortAudioEngine())
    {
    }

    public PortAudioPlayer(IAudioDecoder decoder, IChannelRouter router, EngineOptions options, IPortAudioEngine portAudioEngine)
    {
        _decoder = decoder;
        _router = router;
        _options = options;
        _pa = portAudioEngine;
        _deviceChannels = Math.Max(1, Math.Max(options.Routing?.Tracks?.DefaultIfEmpty(0).Max() ?? 0, 2));
    }

    public Task StopAsync(CancellationToken cancellationToken = default)
    {
        lock (_gate)
        {
            if (_playCts is null) return Task.CompletedTask;
            try { _playCts.Cancel(); } catch { }
        }
        return Task.CompletedTask;
    }

    public Task PlayAsync(string url, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(url)) throw new ArgumentException("URL is required", nameof(url));
        ThrowIfDisposed();
        lock (_gate)
        {
            if (IsPlaying) return Task.CompletedTask;
            _playCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            var token = _playCts.Token;
            _playTask = Task.Run(() => RunAsync(url, token), token);
        }
        return Task.CompletedTask;
    }

    private async Task RunAsync(string url, CancellationToken token)
    {
        try
        {
            _decoder.Open(url);
            int sampleRate = _decoder.SampleRate > 0 ? _decoder.SampleRate : 48000;
            int inChannels = _decoder.Channels > 0 ? _decoder.Channels : 2;

            int framesPerBuffer = 512;
            var src = new float[framesPerBuffer * inChannels];
            var dst = new float[framesPerBuffer * _deviceChannels];

            _pa.Open(sampleRate, _deviceChannels);
            _pa.Start();

            lock (_gate) { IsPlaying = true; }
            Started?.Invoke(this, EventArgs.Empty);

            while (!token.IsCancellationRequested)
            {
                int frames = _decoder.Read(src, framesPerBuffer);
                if (frames <= 0)
                {
                    break; // end of stream
                }
                _router.Route(src, frames, inChannels, dst, _deviceChannels);

                await _pa.WriteAsync(dst, frames, _deviceChannels, sampleRate, token);
            }
        }
        catch (Exception ex)
        {
            Error?.Invoke(this, ex);
        }
        finally
        {
            try { _pa.Stop(); } catch { }
            try { _pa.Close(); } catch { }
            try { _decoder.Close(); } catch { }
            bool cancelled = token.IsCancellationRequested;
            lock (_gate)
            {
                IsPlaying = false;
                _playTask = null;
                _playCts?.Dispose();
                _playCts = null;
            }
            Stopped?.Invoke(this, cancelled);
        }
    }

    private void ThrowIfDisposed()
    {
        if (_disposed) throw new ObjectDisposedException(nameof(PortAudioPlayer));
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        try { _playCts?.Cancel(); } catch { }
        try { _playTask?.Wait(50); } catch { }
        try { _decoder.Close(); } catch { }
        try { _pa.Stop(); } catch { }
        try { _pa.Close(); } catch { }
    }
}
