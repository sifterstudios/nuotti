namespace Nuotti.AudioEngine.AudioDevices;

public sealed record DeviceInfo(string Id, string Name, int Channels);

public sealed record DeviceListResult(string DefaultDeviceId, IReadOnlyList<DeviceInfo> Devices);
