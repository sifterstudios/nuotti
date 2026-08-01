using FluentAssertions;
using Nuotti.Contracts.V1.Qualification;
using Nuotti.SimKit.Qualification;
using Nuotti.SimKit.Trace;
using Xunit;

namespace Nuotti.SimKit.Tests.Trace;

public class ScheduleAndTraceTests
{
    [Fact]
    public void Schedule_generator_is_deterministic_for_seed()
    {
        var a = new ScheduleGenerator(42).Generate(20);
        var b = new ScheduleGenerator(42).Generate(20);
        a.Should().Equal(b);
        a.Should().HaveCount(20);
        a.Select(x => x.Kind).Should().Contain("Reconnect");
    }

    [Fact]
    public void Trace_sink_bounds_and_emits_minimized_replay()
    {
        var sink = new JsonlTraceSink(capacity: 3);
        sink.Record("Join", "A001");
        sink.Record("SubmitAnswer", "A001");
        sink.Record("Reconnect", "A002");
        sink.Record("Reveal", "round-1");
        sink.Count.Should().Be(3);
        var replay = sink.RenderMinimizedReplay();
        replay.Should().Contain("minimized-replay");
        replay.Should().Contain("Reveal");
        replay.Should().NotContain("\"Kind\":\"Join\"");
    }

    [Fact]
    public void Burst250_profile_and_pr_schedule_budget()
    {
        var profile = Burst250LoadProfile.Default;
        profile.DeviceCount.Should().Be(250);
        profile.ReconnectCount.Should().Be(50);
        profile.FinalBurstChangeCount.Should().Be(125);

        var sink = new JsonlTraceSink();
        var schedules = Enumerable.Range(0, 25)
            .Select(seed => new ScheduleGenerator(seed).Generate(8))
            .ToArray();
        schedules.Should().HaveCount(25);
        foreach (var schedule in schedules)
            sink.Record("schedule", $"actions={schedule.Count}");
        sink.RenderMinimizedReplay().Should().Contain("schedule");
        LoadThresholds.MinimizedTraceEventCap.Should().Be(64);
    }
}
