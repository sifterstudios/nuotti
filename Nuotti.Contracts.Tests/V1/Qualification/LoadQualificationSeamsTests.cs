using FluentAssertions;
using Nuotti.Contracts.V1.Qualification;
using Xunit;

namespace Nuotti.Contracts.Tests.V1.Qualification;

public class LoadQualificationSeamsTests
{
    [Fact]
    public void Load_gates_pass_when_ack_and_fanout_within_thresholds()
    {
        var ack = Enumerable.Repeat(40.0, 200).Concat(Enumerable.Repeat(120.0, 40)).Concat([200.0, 220.0]).ToArray();
        var fanOut = Enumerable.Repeat(80.0, 240).Concat([300.0]).ToArray();
        var result = LoadGateEvaluator.Evaluate(ack, fanOut);
        result.Passed.Should().BeTrue(result.FailureReason);
        result.AckP95Ms.Should().BeLessThanOrEqualTo(LoadThresholds.AckLatencyP95Ms);
        result.AckP99Ms.Should().BeLessThanOrEqualTo(LoadThresholds.AckLatencyP99Ms);
    }

    [Fact]
    public void Load_gates_fail_when_p95_exceeds_threshold()
    {
        var ack = Enumerable.Repeat(300.0, 100).ToArray();
        var result = LoadGateEvaluator.Evaluate(ack);
        result.Passed.Should().BeFalse();
        result.FailureReason.Should().Contain("p95");
    }

    [Fact]
    public void Threshold_constants_match_release_gates()
    {
        LoadThresholds.BurstDeviceCount.Should().Be(250);
        LoadThresholds.AckLatencyP95Ms.Should().Be(250);
        LoadThresholds.AckLatencyP99Ms.Should().Be(500);
        LoadThresholds.GeneratedSchedulesPerPr.Should().Be(1000);
        LoadThresholds.GeneratedSchedulesNightly.Should().Be(10_000);
    }
}
