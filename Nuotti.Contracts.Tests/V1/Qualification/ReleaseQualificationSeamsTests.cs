using FluentAssertions;
using Nuotti.Contracts.V1.Qualification;
using Xunit;

namespace Nuotti.Contracts.Tests.V1.Qualification;

public class ReleaseQualificationSeamsTests
{
    [Fact]
    public void Hil_and_start_gates_pass_within_thresholds()
    {
        ReleaseGateEvaluator.EvaluateHil(new HilMeasurement(0, 40, AfterCorrection: false, 5, 1))
            .Passed.Should().BeTrue();
        ReleaseGateEvaluator.EvaluateHil(new HilMeasurement(0, 140, AfterCorrection: true, 5, 1))
            .Passed.Should().BeTrue();
        ReleaseGateEvaluator.EvaluateStart(new StartMeasurement(0.9, 6, 128, 48_000))
            .Passed.Should().BeTrue();
    }

    [Fact]
    public void Hil_and_start_gates_fail_outside_thresholds()
    {
        ReleaseGateEvaluator.EvaluateHil(new HilMeasurement(2, 0, false, 0, 1))
            .Passed.Should().BeFalse();
        ReleaseGateEvaluator.EvaluateHil(new HilMeasurement(0, 120, AfterCorrection: false, 0, 1))
            .Passed.Should().BeFalse();
        ReleaseGateEvaluator.EvaluateStart(new StartMeasurement(2.0, 0, 128, 48_000))
            .Passed.Should().BeFalse();
    }

    [Fact]
    public void Compressed_soaks_meet_duration_and_round_gates()
    {
        var runner = new CompressedSoakRunner();
        var cloud = runner.RunCloudSoak(TimeSpan.FromMinutes(4));
        var cloudGate = ReleaseGateEvaluator.EvaluateSoak(
            cloud, ReleaseThresholds.CloudSoakDuration, ReleaseThresholds.CloudSoakMinRounds);
        cloudGate.Passed.Should().BeTrue(cloudGate.Detail);
        cloud.Participants.Should().Be(200);
        cloud.RoundsCompleted.Should().BeGreaterThanOrEqualTo(40);

        var hardware = runner.RunHardwareRehearsal(TimeSpan.FromMinutes(5));
        var hwGate = ReleaseGateEvaluator.EvaluateSoak(
            hardware, ReleaseThresholds.HardwareRehearsalPerAsio, minRounds: 10);
        hwGate.Passed.Should().BeTrue(hwGate.Detail);
    }

    [Fact]
    public void Release_report_is_build_linked_and_rejects_waivers_for_correctness()
    {
        var report = new ReleaseQualificationReport
        {
            BuildId = "local-ci",
            CommitSha = "abc1234"
        };
        report.AddGate(ReleaseGateEvaluator.EvaluateHil(new HilMeasurement(0, 10, false, 1, 0.5)));
        report.AddGate(ReleaseGateEvaluator.EvaluateStart(new StartMeasurement(0.5, 2, 64, 48_000)));
        report.TryAddWaiver("please ignore projector skew");
        report.AllCorrectnessGatesPassed.Should().BeTrue();
        report.Waivers.Should().ContainSingle();
        report.RenderMarkdown().Should().Contain("Build: `local-ci`");
        report.RenderMarkdown().Should().Contain("Waiver attempts");
        report.RenderJson().Should().Contain("\"allCorrectnessGatesPassed\": true");
    }

    [Fact]
    public void Failed_gate_cannot_be_waived_into_a_pass()
    {
        var report = new ReleaseQualificationReport { BuildId = "b", CommitSha = "c" };
        report.AddGate(new GateEvaluation("hil.projector", false, "skew"));
        report.TryAddWaiver("ops approved");
        report.AllCorrectnessGatesPassed.Should().BeFalse();
    }
}
