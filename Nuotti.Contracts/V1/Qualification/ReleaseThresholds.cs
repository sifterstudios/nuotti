namespace Nuotti.Contracts.V1.Qualification;

/// <summary>
/// Hardware-in-the-loop, start, and soak thresholds from the production MVP gates (#243 / #264).
/// </summary>
public static class ReleaseThresholds
{
    public const double ProjectorSteadySkewMs = 100;
    public const double ProjectorAfterCorrectionSkewMs = 150;
    public const double TapToFirstSampleP95Seconds = 1.25;
    public const double ScheduledStartSlackBuffers = 1;
    public const double ScheduledStartSlackMs = 5;

    public static readonly TimeSpan HardwareRehearsalPerAsio = TimeSpan.FromMinutes(90);
    public static readonly TimeSpan CloudSoakDuration = TimeSpan.FromHours(3);
    public const int CloudSoakParticipants = 200;
    public const int CloudSoakMinRounds = 40;

    public static readonly int[] AsioBufferFrames = [64, 128, 256, 512];
}

public sealed record HilMeasurement(
    double BackingClickSkewFrames,
    double ProjectorSkewMs,
    bool AfterCorrection,
    double DriftMsPerMinute,
    double StopLatencyBuffers);

public sealed record StartMeasurement(
    double TapToFirstSampleSeconds,
    double ScheduledToAsioStartMs,
    int FramesPerBuffer,
    double SampleRate);

public sealed record SoakProgress(
    TimeSpan Elapsed,
    int Participants,
    int RoundsCompleted,
    double PeakMemoryMb,
    double EventBacklog,
    bool ManualRestartRequired);

public sealed record GateEvaluation(string Name, bool Passed, string? Detail);

public static class ReleaseGateEvaluator
{
    public static GateEvaluation EvaluateHil(HilMeasurement m)
    {
        if (Math.Abs(m.BackingClickSkewFrames) > 0.5)
            return new("hil.alignment", false, $"backing/click skew {m.BackingClickSkewFrames} frames");

        var limit = m.AfterCorrection
            ? ReleaseThresholds.ProjectorAfterCorrectionSkewMs
            : ReleaseThresholds.ProjectorSteadySkewMs;
        if (Math.Abs(m.ProjectorSkewMs) > limit)
            return new("hil.projector", false, $"projector skew {m.ProjectorSkewMs:F1}ms > {limit}ms");

        if (Math.Abs(m.DriftMsPerMinute) > ReleaseThresholds.ProjectorSteadySkewMs)
            return new("hil.drift", false, $"drift {m.DriftMsPerMinute:F1}ms/min unbounded");

        if (m.StopLatencyBuffers > ReleaseThresholds.ScheduledStartSlackBuffers + 0.01)
            return new("hil.stop", false, $"stop took {m.StopLatencyBuffers:F2} buffers");

        return new("hil", true, null);
    }

    public static GateEvaluation EvaluateStart(StartMeasurement m)
    {
        if (m.TapToFirstSampleSeconds > ReleaseThresholds.TapToFirstSampleP95Seconds)
            return new("start.tap", false,
                $"tap-to-first-sample {m.TapToFirstSampleSeconds:F3}s > {ReleaseThresholds.TapToFirstSampleP95Seconds}s");

        var bufferMs = m.FramesPerBuffer / m.SampleRate * 1000.0;
        var allowed = bufferMs * ReleaseThresholds.ScheduledStartSlackBuffers + ReleaseThresholds.ScheduledStartSlackMs;
        if (Math.Abs(m.ScheduledToAsioStartMs) > allowed)
            return new("start.scheduled", false,
                $"scheduled→ASIO {m.ScheduledToAsioStartMs:F1}ms > {allowed:F1}ms");

        return new("start", true, null);
    }

    public static GateEvaluation EvaluateSoak(SoakProgress progress, TimeSpan requiredDuration, int minRounds)
    {
        if (progress.Elapsed + TimeSpan.FromSeconds(1) < requiredDuration)
            return new("soak.duration", false, $"elapsed {progress.Elapsed} < {requiredDuration}");
        if (progress.RoundsCompleted < minRounds)
            return new("soak.rounds", false, $"rounds {progress.RoundsCompleted} < {minRounds}");
        if (progress.ManualRestartRequired)
            return new("soak.restart", false, "manual restart required");
        if (progress.EventBacklog > 1_000)
            return new("soak.backlog", false, $"event backlog {progress.EventBacklog}");
        return new("soak", true, null);
    }
}
