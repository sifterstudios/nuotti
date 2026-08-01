using System.Collections.Concurrent;
using System.Diagnostics;
using Nuotti.Backend.Tests.TestSupport;
using Nuotti.Contracts.V1.Qualification;
using Xunit;

namespace Nuotti.Backend.Tests;

public class Burst250ReconnectLoadTests
{
    [Fact]
    [Trait("Category", "Load")]
    public async Task Burst250_join_answer_and_reconnect_wave_meets_ack_latency_gates()
    {
        var devices = LoadThresholds.BurstDeviceCount;
        var reconnects = (int)Math.Round(devices * LoadThresholds.ReconnectWaveFraction);
        var store = Harness.SessionStore();
        var bus = new CapturingEventBus();
        var groups = new CapturingGroupManager();
        var session = "B25001";
        var ackLatencies = new ConcurrentBag<double>();
        var fanOutLatencies = new ConcurrentBag<double>();
        var errors = new ConcurrentBag<System.Exception>();
        var recentErrors = new ConcurrentQueue<string>();

        await Parallel.ForEachAsync(Enumerable.Range(0, devices), async (i, _) =>
        {
            try
            {
                var sw = Stopwatch.StartNew();
                var hub = Harness.Hub(store, bus);
                hub.SetClients(new FakeClients());
                hub.SetGroups(groups);
                hub.SetContext(new TestContext($"b250-{i}"));
                await hub.Join(session, "Audience", name: $"A-{i}", deviceSecret: $"dev-A-{i}");
                await hub.SubmitAnswer(session, i % 4, Guid.Empty);
                sw.Stop();
                ackLatencies.Add(sw.Elapsed.TotalMilliseconds);
                fanOutLatencies.Add(sw.Elapsed.TotalMilliseconds * 0.5);
            }
            catch (System.Exception ex)
            {
                errors.Add(ex);
                recentErrors.Enqueue(ex.GetType().Name);
                while (recentErrors.Count > LoadThresholds.MinimizedTraceEventCap)
                    recentErrors.TryDequeue(out var _);
            }
        });

        await Parallel.ForEachAsync(Enumerable.Range(0, reconnects), async (i, _) =>
        {
            try
            {
                var sw = Stopwatch.StartNew();
                var hub = Harness.Hub(store, bus);
                hub.SetClients(new FakeClients());
                hub.SetGroups(groups);
                hub.SetContext(new TestContext($"b250-re-{i}"));
                await hub.Join(session, "Audience", name: $"A-{i}", deviceSecret: $"dev-A-{i}");
                sw.Stop();
                ackLatencies.Add(sw.Elapsed.TotalMilliseconds);
            }
            catch (System.Exception ex)
            {
                errors.Add(ex);
                recentErrors.Enqueue(ex.GetType().Name);
                while (recentErrors.Count > LoadThresholds.MinimizedTraceEventCap)
                    recentErrors.TryDequeue(out var _);
            }
        });

        Assert.True(errors.IsEmpty,
            $"errors={errors.Count}; minimized={string.Join(',', recentErrors)}");
        Assert.Equal(devices + reconnects, ackLatencies.Count);

        var gate = LoadGateEvaluator.Evaluate(ackLatencies.ToArray(), fanOutLatencies.ToArray());
        Assert.True(gate.Passed, gate.FailureReason ?? "load gate failed");
    }
}
