using System.ComponentModel;
using System.Diagnostics;
namespace Nuotti.AudioEngine.Playback;

internal sealed class RealProcessHandle : IProcessHandle
{
    private readonly Process _process;

    public RealProcessHandle(Process process)
    {
        _process = process;
        _process.EnableRaisingEvents = true;
        _process.Exited += (_, __) => Exited?.Invoke(this, EventArgs.Empty);
    }

    public bool HasExited => _process.HasExited;

    public event EventHandler? Exited;

    public bool Start() => _process.Start();

    public void Kill(bool entireProcessTree)
    {
        try { _process.Kill(entireProcessTree); } catch { }
    }

    public void Dispose()
    {
        try { _process.Dispose(); } catch { }
    }

    public static RealProcessHandle FromStartInfo(ProcessStartInfo psi)
    {
        var p = new Process { StartInfo = psi };
        return new RealProcessHandle(p);
    }
}

public sealed class RealProcessRunner : IProcessRunner
{
    public IProcessHandle Create(ProcessStartInfo startInfo, bool enableRaisingEvents = true)
    {
        var process = new Process { StartInfo = startInfo, EnableRaisingEvents = enableRaisingEvents };
        return new RealProcessHandle(process);
    }

    public bool CanStart(string fileName)
    {
        try
        {
            using var p = new Process();
            p.StartInfo = new ProcessStartInfo
            {
                FileName = fileName,
                Arguments = "--version",
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true,
            };
            if (!p.Start()) return false;
            try { if (!p.WaitForExit(300)) p.Kill(true); } catch { }
            return true;
        }
        catch (Win32Exception)
        {
            return false;
        }
        catch
        {
            return true;
        }
    }
}
