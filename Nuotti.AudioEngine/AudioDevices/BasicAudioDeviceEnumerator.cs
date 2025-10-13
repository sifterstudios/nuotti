using System.Runtime.InteropServices;
namespace Nuotti.AudioEngine.AudioDevices;

/// <summary>
/// Minimal, dependency-free device enumerator. For now, returns a single default stereo device.
/// Placeholder for future NAudio/PortAudio backends.
/// </summary>
public sealed class BasicAudioDeviceEnumerator : IAudioDeviceEnumerator
{
    public Task<DeviceListResult> EnumerateAsync(CancellationToken cancellationToken = default)
    {
        // Try to provide a sensible default label based on OS
        var os = RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? "Windows" :
                 RuntimeInformation.IsOSPlatform(OSPlatform.OSX) ? "macOS" :
                 RuntimeInformation.IsOSPlatform(OSPlatform.Linux) ? "Linux" : "UnknownOS";
        var defaultId = "default";
        var devices = new List<DeviceInfo>
        {
            new DeviceInfo(defaultId, $"System Default ({os})", 2)
        };
        return Task.FromResult(new DeviceListResult(defaultId, devices));
    }
}
