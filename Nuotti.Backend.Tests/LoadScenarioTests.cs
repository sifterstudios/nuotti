using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Nuotti.Backend.Models;
using Nuotti.Backend.Sessions;
using Nuotti.Contracts.V1.Event;
using Nuotti.Contracts.V1.Eventing;
using System.Collections.Concurrent;
using System.Security.Claims;
using Nuotti.Backend.Tests.TestSupport;
namespace Nuotti.Backend.Tests;

public class LoadScenarioTests
{
    static InMemorySessionStore CreateSessionStore() => Harness.SessionStore();

    [Theory]
    [InlineData(200)]
    [InlineData(500)]
    public async Task Backend_responsive_under_load_and_error_rate_below_1_percent(int audiences)
    {
        // Arrange a shared store and bus to simulate a real backend state under concurrent access
        var store = CreateSessionStore();
        var bus = new CapturingEventBus();
        var groups = new CapturingGroupManager();
        var session = "LOAD01";

        var errors = new ConcurrentBag<System.Exception>();

        // Act: run concurrent join+submit cycles
        await Parallel.ForEachAsync(Enumerable.Range(0, audiences), async (i, ct) =>
        {
            try
            {
                var hub = Harness.Hub(store, bus);
                var clients = new FakeClients();
                hub.SetClients(clients);
                hub.SetGroups(groups);
                var ctx = new TestContext($"load-aud-{i}");
                hub.SetContext(ctx);

                await hub.Join(session, "Audience", name: $"A-{i}", deviceSecret: $"dev-A-{i}");
                await hub.SubmitAnswer(session, i % 4, Guid.Empty);
            }
            catch (System.Exception ex)
            {
                errors.Add(ex);
            }
        });

        // Assert: error rate < 1%
        double errorRate = audiences == 0 ? 0 : (double)errors.Count / audiences;
        Assert.True(errorRate < 0.01, $"Error rate {errorRate:P2} exceeded 1%. errors={errors.Count} of {audiences}");
    }
}
