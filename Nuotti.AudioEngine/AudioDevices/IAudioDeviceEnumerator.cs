namespace Nuotti.AudioEngine.AudioDevices;

public interface IAudioDeviceEnumerator
{
    Task<DeviceListResult> EnumerateAsync(CancellationToken cancellationToken = default);
}
