using Nuotti.Backend.Eventing;
using Nuotti.Contracts.V1.Eventing;
namespace Nuotti.Backend.Tests;

public class EventBusTests
{
    sealed record DummyEvent();

    [Fact]
    public async Task Publish_ReachesAllSubscribers()
    {
        IEventBus bus = new InMemoryEventBus();
        var hits = 0;
        bus.Subscribe<DummyEvent>(async (_, _) => { Interlocked.Increment(ref hits); await Task.Yield(); });
        bus.Subscribe<DummyEvent>(async (_, ct) => { Interlocked.Increment(ref hits); await Task.Delay(1, ct); });

        await bus.PublishAsync(new DummyEvent());

        Assert.Equal(2, hits);
    }
    private static readonly string[] expected = new[]{"A","B","C"};

    [Fact]
    public async Task Publish_InvokesInSubscriptionOrder()
    {
        IEventBus bus = new InMemoryEventBus();
        var order = new List<string>();
        bus.Subscribe<DummyEvent>(async (_, _) => { order.Add("A"); await Task.Yield(); });
        bus.Subscribe<DummyEvent>(async (_, _) => { order.Add("B"); await Task.Yield(); });
        bus.Subscribe<DummyEvent>(async (_, _) => { order.Add("C"); await Task.Yield(); });

        await bus.PublishAsync(new DummyEvent());

        Assert.Equal(expected, order);
    }
}