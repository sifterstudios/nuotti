using System.ComponentModel;
using System.Diagnostics;
namespace Nuotti.AudioEngine.Playback;

public sealed class SystemPlayer : IAudioPlayer, IDisposable
{
    private readonly PreferredPlayer _preferred;
    private readonly Func<string, PreferredPlayer, (string fileName, string args)?> _resolver;
    private readonly IProcessRunner _runner;
    private IProcessHandle? _process;
    private readonly object _gate = new();
    private bool _disposed;
    private volatile bool _stopRequested;

    public event EventHandler? Started;
    public event EventHandler<bool>? Stopped; // bool = cancelled
    public event EventHandler<Exception>? Error;

    public bool IsPlaying { get; private set; }

    public SystemPlayer(PreferredPlayer preferred = PreferredPlayer.Auto, Func<string, PreferredPlayer, (string fileName, string args)?>? resolver = null, IProcessRunner? runner = null)
    {
        _preferred = preferred;
        _runner = runner ?? new RealProcessRunner();
        _resolver = resolver ?? ((url, p) => BuildPlayerCommand(url, p));
    }

    public async Task PlayAsync(string url, CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();
        if (string.IsNullOrWhiteSpace(url)) throw new ArgumentException("URL is required", nameof(url));

        lock (_gate)
        {
            if (IsPlaying)
                return; // ignore duplicate calls for now
        }

        var spec = _resolver(url, _preferred);
        if (spec is null)
        {
            Error?.Invoke(this, new InvalidOperationException("No supported player found (afplay/ffplay/vlc)"));
            return;
        }

        var (fileName, args) = spec.Value;
        var psi = new ProcessStartInfo
        {
            FileName = fileName,
            Arguments = args,
            UseShellExecute = false,
            RedirectStandardOutput = false,
            RedirectStandardError = false,
            CreateNoWindow = true,
        };

        var p = _runner.Create(psi, enableRaisingEvents: true);
        p.Exited += (_, __) =>
        {
            bool cancelledByStop;
            lock (_gate)
            {
                cancelledByStop = _stopRequested;
                _stopRequested = false; // reset for next run
                IsPlaying = false;
                _process = null;
            }
            Stopped?.Invoke(this, cancelledByStop);
            p.Dispose();
        };

        try
        {
            if (!p.Start())
            {
                Error?.Invoke(this, new InvalidOperationException("Failed to start player process"));
                p.Dispose();
                return;
            }
            lock (_gate)
            {
                _process = p;
                IsPlaying = true;
            }
            Started?.Invoke(this, EventArgs.Empty);

            // If caller cancels before process exits, stop it.
            if (cancellationToken.CanBeCanceled)
            {
                cancellationToken.Register(() =>
                {
                    lock (_gate)
                    {
                        if (_process != null && !_process.HasExited)
                        {
                            _stopRequested = true;
                            try { _process.Kill(true); } catch { /* ignore */ }
                        }
                    }
                });
            }
        }
        catch (Exception ex) when (ex is Win32Exception or InvalidOperationException)
        {
            Error?.Invoke(this, ex);
            try { p.Dispose(); } catch { }
        }
    }

    public Task StopAsync(CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();
        lock (_gate)
        {
            if (_process is { HasExited: false })
            {
                _stopRequested = true;
                try { _process.Kill(true); } catch { /* ignore */ }
            }
        }
        return Task.CompletedTask;
    }

    private (string fileName, string args)? BuildPlayerCommand(string url, PreferredPlayer preferred)
    {
        var quotedUrl = '"' + url + '"';

        static (string file, string args)? TryMap(PreferredPlayer p, string q) => p switch
        {
            PreferredPlayer.Afplay => ("afplay", q),
            PreferredPlayer.Ffplay => ("ffplay", $"-nodisp -autoexit {q}"),
            PreferredPlayer.Vlc => ("vlc", $"--play-and-exit {q}"),
            _ => null
        };

        var attempts = new List<(string file, string args)>();
        var pref = TryMap(preferred, quotedUrl);
        if (pref is not null) attempts.Add(pref.Value);

        if (OperatingSystem.IsMacOS())
        {
            attempts.Add(("afplay", quotedUrl));
            attempts.Add(("ffplay", $"-nodisp -autoexit {quotedUrl}"));
            attempts.Add(("vlc", $"--play-and-exit {quotedUrl}"));
        }
        else if (OperatingSystem.IsWindows())
        {
            attempts.Add(("ffplay", $"-nodisp -autoexit {quotedUrl}"));
            attempts.Add(("vlc", $"--play-and-exit {quotedUrl}"));
            attempts.Add(("powershell", $"-Command Start-Process {quotedUrl}"));
        }
        else
        {
            attempts.Add(("ffplay", $"-nodisp -autoexit {quotedUrl}"));
            attempts.Add(("vlc", $"--play-and-exit {quotedUrl}"));
        }

        foreach (var a in attempts)
        {
            if (_runner.CanStart(a.file)) return (a.file, a.args);
        }
        return null;
    }


    private void ThrowIfDisposed()
    {
        if (_disposed) throw new ObjectDisposedException(nameof(SystemPlayer));
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        try
        {
            lock (_gate)
            {
                if (_process is { HasExited: false })
                {
                    // Ensure no orphan processes are left behind
                    _stopRequested = true;
                    try { _process.Kill(true); } catch { /* ignore */ }
                }
            }
        }
        catch { }
        finally
        {
            try { _process?.Dispose(); } catch { }
            _process = null;
            IsPlaying = false;
        }
    }
}