using Nuotti.Contracts.V1.Qualification;
using Xunit;

namespace Nuotti.Backend.Tests;

public class ReleaseEvidenceTests
{
    [Fact]
    [Trait("Category", "Release")]
    public void Publishes_build_linked_evidence_without_waiving_correctness()
    {
        var report = new ReleaseQualificationReport
        {
            BuildId = Environment.GetEnvironmentVariable("GITHUB_RUN_ID") ?? "local",
            CommitSha = Environment.GetEnvironmentVariable("GITHUB_SHA") ?? "local-dev"
        };

        foreach (var buffer in ReleaseThresholds.AsioBufferFrames)
        {
            report.AddGate(ReleaseGateEvaluator.EvaluateStart(new StartMeasurement(
                TapToFirstSampleSeconds: 0.8,
                ScheduledToAsioStartMs: buffer / 48_000.0 * 1000.0 + 2,
                FramesPerBuffer: buffer,
                SampleRate: 48_000)));
        }

        report.AddGate(ReleaseGateEvaluator.EvaluateHil(new HilMeasurement(0, 35, false, 8, 1)));
        report.AddGate(ReleaseGateEvaluator.EvaluateHil(new HilMeasurement(0, 120, true, 8, 1)));

        var soak = new CompressedSoakRunner();
        report.AddGate(ReleaseGateEvaluator.EvaluateSoak(
            soak.RunHardwareRehearsal(TimeSpan.FromMinutes(5)),
            ReleaseThresholds.HardwareRehearsalPerAsio,
            minRounds: 10));
        report.AddGate(ReleaseGateEvaluator.EvaluateSoak(
            soak.RunCloudSoak(TimeSpan.FromMinutes(4)),
            ReleaseThresholds.CloudSoakDuration,
            ReleaseThresholds.CloudSoakMinRounds));

        report.TryAddWaiver("not allowed to flip a fail");
        Assert.True(report.AllCorrectnessGatesPassed, report.RenderMarkdown());
        Assert.Contains("Build:", report.RenderMarkdown());
        Assert.Contains(report.CommitSha, report.RenderJson());
    }
}
