using Nuotti.AudioEngine.Playback;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;
namespace Nuotti.AudioEngine.Tests.Fakes;

public sealed class FakeProcessHandle : IProcessHandle
{
    private readonly Action<FakeProcessHandle>? _onStart;
    private int _hasExited;
    public FakeProcessHandle(ProcessStartInfo startInfo, Action<FakeProcessHandle>? onStart = null)
    {
        StartInfo = startInfo;
        _onStart = onStart;
    }

    public ProcessStartInfo StartInfo { get; }

    public bool HasExited => _hasExited == 1;

    public event EventHandler? Exited;

    public bool Start()
    {
        _onStart?.Invoke(this);
        return true;
    }

    public void Kill(bool entireProcessTree)
    {
        // Simulate termination
        if (Interlocked.Exchange(ref _hasExited, 1) == 1) return;
        Exited?.Invoke(this, EventArgs.Empty);
    }

    public void CompleteNormally()
    {
        if (Interlocked.Exchange(ref _hasExited, 1) == 1) return;
        Exited?.Invoke(this, EventArgs.Empty);
    }

    public void Dispose() { }
}

public sealed class FakeProcessRunner : IProcessRunner
{
    private readonly HashSet<string> _canStart = new(StringComparer.OrdinalIgnoreCase);
    public readonly List<FakeProcessHandle> Started = new();
    private readonly Action<FakeProcessHandle>? _onStart;

    public FakeProcessRunner(IEnumerable<string>? available = null, Action<FakeProcessHandle>? onStart = null)
    {
        if (available != null)
        {
            foreach (var f in available) _canStart.Add(f);
        }
        _onStart = onStart;
    }

    public IProcessHandle Create(ProcessStartInfo startInfo, bool enableRaisingEvents = true)
    {
        var h = new FakeProcessHandle(startInfo, h =>
        {
            Started.Add(h);
            _onStart?.Invoke(h);
        });
        return h;
    }

    public bool CanStart(string fileName) => _canStart.Contains(fileName);
}
