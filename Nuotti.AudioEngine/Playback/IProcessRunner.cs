using System.Diagnostics;
namespace Nuotti.AudioEngine.Playback;

public interface IProcessHandle : IDisposable
{
    bool HasExited { get; }
    event EventHandler? Exited;
    bool Start();
    void Kill(bool entireProcessTree);
}

public interface IProcessRunner
{
    IProcessHandle Create(ProcessStartInfo startInfo, bool enableRaisingEvents = true);
    bool CanStart(string fileName);
}
