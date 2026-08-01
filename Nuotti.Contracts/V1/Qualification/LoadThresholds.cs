namespace Nuotti.Contracts.V1.Qualification;

/// <summary>
/// Release qualification thresholds from the production MVP gates (#243 / #263).
/// </summary>
public static class LoadThresholds
{
    public const int BurstDeviceCount = 250;
    public const int JoinWindowSeconds = 30;
    public const double ReconnectWaveFraction = 0.20;
    public const double FinalBurstChangeFraction = 0.50;

    public const double AckLatencyP95Ms = 250;
    public const double AckLatencyP99Ms = 500;
    public const double PhaseFanOutP95Ms = 500;

    public const int GeneratedSchedulesPerPr = 1_000;
    public const int GeneratedSchedulesNightly = 10_000;
    public const int MinimizedTraceEventCap = 64;
}

/// <summary>
/// Result of comparing measured samples against <see cref="LoadThresholds"/>.
/// </summary>
public sealed record LoadGateResult(
    bool Passed,
    double AckP95Ms,
    double AckP99Ms,
    double FanOutP95Ms,
    string? FailureReason);

public static class LoadGateEvaluator
{
    public static LoadGateResult Evaluate(
        IReadOnlyList<double> ackLatenciesMs,
        IReadOnlyList<double>? fanOutLatenciesMs = null)
    {
        var ackP95 = Percentile(ackLatenciesMs, 95);
        var ackP99 = Percentile(ackLatenciesMs, 99);
        var fanOut = Percentile(fanOutLatenciesMs ?? ackLatenciesMs, 95);

        if (ackLatenciesMs.Count == 0)
            return new LoadGateResult(false, 0, 0, 0, "no acknowledgement samples");

        if (ackP95 > LoadThresholds.AckLatencyP95Ms)
            return new LoadGateResult(false, ackP95, ackP99, fanOut,
                $"ack p95 {ackP95:F1}ms exceeds {LoadThresholds.AckLatencyP95Ms}ms");

        if (ackP99 > LoadThresholds.AckLatencyP99Ms)
            return new LoadGateResult(false, ackP95, ackP99, fanOut,
                $"ack p99 {ackP99:F1}ms exceeds {LoadThresholds.AckLatencyP99Ms}ms");

        if (fanOut > LoadThresholds.PhaseFanOutP95Ms)
            return new LoadGateResult(false, ackP95, ackP99, fanOut,
                $"fan-out p95 {fanOut:F1}ms exceeds {LoadThresholds.PhaseFanOutP95Ms}ms");

        return new LoadGateResult(true, ackP95, ackP99, fanOut, null);
    }

    public static double Percentile(IReadOnlyList<double> samples, double p)
    {
        if (samples.Count == 0) return 0;
        var arr = samples.ToArray();
        Array.Sort(arr);
        var rank = (p / 100.0) * (arr.Length - 1);
        var low = (int)Math.Floor(rank);
        var high = (int)Math.Ceiling(rank);
        if (low == high) return arr[low];
        var weight = rank - low;
        return arr[low] * (1 - weight) + arr[high] * weight;
    }
}
