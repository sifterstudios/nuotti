using Microsoft.Extensions.Options;
using Nuotti.Backend.Models;
using Nuotti.Backend.Sessions;
namespace Nuotti.Backend.Tests;

public class SessionStoreTests
{
    static InMemorySessionStore CreateStore(FakeTimeProvider time, int idleSeconds = 60)
    {
        var options = Options.Create(new NuottiOptions
        {
            SessionIdleTimeoutSeconds = idleSeconds,
            SessionEvictionIntervalSeconds = 3600 // large; we will trigger eviction manually
        });
        return new InMemorySessionStore(options, time);
    }

    [Fact]
    public void Touch_AddsConnections_And_Remove_UpdatesCounts()
    {
        var time = new FakeTimeProvider(DateTimeOffset.UtcNow);
        using var store = CreateStore(time);

        store.Touch("dev", "performer", "p1");
        store.Touch("dev", "projector", "pr1");
        store.Touch("dev", "engine", "e1");
        store.Touch("dev", "audience", "a1", "Alice");
        store.Touch("dev", "audience", "a2", "Bob");

        var counts = store.GetCounts("dev");
        Assert.Equal(1, counts.Performer);
        Assert.Equal(1, counts.Projector);
        Assert.Equal(1, counts.Engine);
        Assert.Equal(2, counts.Audiences);

        // Remove one audience
        store.Remove("a1");
        counts = store.GetCounts("dev");
        Assert.Equal(1, counts.Audiences);

        // Remove projector
        store.Remove("pr1");
        counts = store.GetCounts("dev");
        Assert.Equal(0, counts.Projector);

        // Removing an unknown connection should be safe
        store.Remove("does-not-exist");
        counts = store.GetCounts("dev");
        Assert.Equal(1, counts.Performer);
        Assert.Equal(1, counts.Engine);
        Assert.Equal(1, counts.Audiences);
    }

    [Fact]
    public void Evicts_Session_After_IdleTimeout()
    {
        var now = DateTimeOffset.Parse("2025-01-01T00:00:00Z");
        var time = new FakeTimeProvider(now);
        using var store = CreateStore(time, idleSeconds: 60);

        store.Touch("s1", "audience", "a1");
        Assert.Equal(1, store.GetCounts("s1").Audiences);

        // Advance just before timeout
        time.Advance(TimeSpan.FromSeconds(59));
        store.EvictIdleNow();
        Assert.Equal(1, store.GetCounts("s1").Audiences);

        // Advance beyond timeout and evict
        time.Advance(TimeSpan.FromSeconds(2));
        store.EvictIdleNow();
        var counts = store.GetCounts("s1");
        Assert.Equal(0, counts.Audiences);
        Assert.Equal(0, counts.Performer);
        Assert.Equal(0, counts.Projector);
        Assert.Equal(0, counts.Engine);
    }
}

internal sealed class FakeTimeProvider(DateTimeOffset start) : TimeProvider
{
    DateTimeOffset _now = start;

    public override DateTimeOffset GetUtcNow() => _now;

    public void Advance(TimeSpan by) => _now = _now.Add(by);

    public override ITimer CreateTimer(TimerCallback callback, object? state, TimeSpan dueTime, TimeSpan period)
        => new NoopTimer();

    sealed class NoopTimer : ITimer
    {
        public bool Change(TimeSpan dueTime, TimeSpan period) => true;
        public void Dispose() { }
        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }
}
