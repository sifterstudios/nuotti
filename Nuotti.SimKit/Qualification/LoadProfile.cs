using Nuotti.Contracts.V1.Qualification;

namespace Nuotti.SimKit.Qualification;

/// <summary>
/// Named 250-device burst/reconnect load profile from release gates.
/// </summary>
public sealed record Burst250LoadProfile(
    int DeviceCount = LoadThresholds.BurstDeviceCount,
    int JoinWindowSeconds = LoadThresholds.JoinWindowSeconds,
    double ReconnectWaveFraction = LoadThresholds.ReconnectWaveFraction,
    double FinalBurstChangeFraction = LoadThresholds.FinalBurstChangeFraction)
{
    public static Burst250LoadProfile Default { get; } = new();

    public int ReconnectCount => (int)Math.Round(DeviceCount * ReconnectWaveFraction);
    public int FinalBurstChangeCount => (int)Math.Round(DeviceCount * FinalBurstChangeFraction);
}
