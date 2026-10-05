using Microsoft.Extensions.Options;
using Nuotti.Backend.Commands;
using Nuotti.Backend.Idempotency;
using Nuotti.Backend.Models;
using Nuotti.Backend.Persistence;
using Nuotti.Backend.Sessions;
using Nuotti.Backend.Tests.TestSupport;
using Nuotti.Contracts.V1.Enum;
using Nuotti.Contracts.V1.Message.Phase;
using Nuotti.Contracts.V1.Model;
using Nuotti.Contracts.V1.Protocol;
using Nuotti.Contracts.V1.Reducer;
using Xunit;

namespace Nuotti.Backend.Tests;

public class SessionStoreTests
{
    static InMemorySessionStore CreateStore(FakeTimeProvider time, IGameStateStore game, int idleSeconds = 60)
    {
        var options = Options.Create(new NuottiOptions
        {
            SessionIdleTimeoutSeconds = idleSeconds,
            SessionEvictionIntervalSeconds = 3600 // large; we will trigger eviction manually
        });
        return new InMemorySessionStore(options, game, time);
    }

    [Fact]
    public void Touch_AddsConnections_And_Remove_UpdatesCounts()
    {
        var time = new FakeTimeProvider(DateTimeOffset.UtcNow);
        using var store = CreateStore(time, new InMemoryGameStateStore());

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
        using var store = CreateStore(time, new InMemoryGameStateStore(), idleSeconds: 60);

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

    [Fact]
    public void Idle_eviction_drops_the_hot_GameStateSnapshot()
    {
        var now = DateTimeOffset.Parse("2025-01-01T00:00:00Z");
        var time = new FakeTimeProvider(now);
        var game = new InMemoryGameStateStore();
        using var store = CreateStore(time, game, idleSeconds: 30);

        store.Touch("s1", "audience", "a1");
        game.Set("s1", GameReducer.Initial("s1") with { Phase = Phase.Guessing, Choices = ["a", "b"] });

        time.Advance(TimeSpan.FromSeconds(31));
        store.EvictIdleNow();

        Assert.False(game.TryGet("s1", out _));
    }

    [Fact]
    public void Clear_drops_the_hot_snapshot_immediately()
    {
        var time = new FakeTimeProvider(DateTimeOffset.UtcNow);
        var game = new InMemoryGameStateStore();
        using var store = CreateStore(time, game);

        store.Touch("s1", "audience", "a1");
        game.Set("s1", GameReducer.Initial("s1") with { Choices = ["x"] });
        store.Clear("s1");
        Assert.False(game.TryGet("s1", out _));
    }

    [Fact]
    public void Last_disconnect_keeps_the_hot_snapshot_until_idle_eviction()
    {
        var now = DateTimeOffset.Parse("2025-01-01T00:00:00Z");
        var time = new FakeTimeProvider(now);
        var game = new InMemoryGameStateStore();
        using var store = CreateStore(time, game, idleSeconds: 30);

        store.Touch("s2", "audience", "a2");
        game.Set("s2", GameReducer.Initial("s2") with { Choices = ["y"] });
        store.Remove("a2");
        Assert.True(game.TryGet("s2", out _));

        time.Advance(TimeSpan.FromSeconds(29));
        store.EvictIdleNow();
        Assert.True(game.TryGet("s2", out _));

        time.Advance(TimeSpan.FromSeconds(2));
        store.EvictIdleNow();
        Assert.False(game.TryGet("s2", out _));
        Assert.Equal(0, store.GetCounts("s2").Audiences);
    }

    [Fact]
    public async Task After_idle_eviction_ApplyAsync_rehydrates_from_durable()
    {
        var now = DateTimeOffset.Parse("2025-01-01T00:00:00Z");
        var time = new FakeTimeProvider(now);
        var game = new InMemoryGameStateStore();
        using var sessions = CreateStore(time, game, idleSeconds: 10);
        var durable = new InMemoryDurableSessionCommitStore();
        var bus = new CapturingEventBus();
        var processor = new SessionCommandProcessor(
            game,
            new InMemoryIdempotencyStore(Options.Create(new NuottiOptions())),
            bus,
            Microsoft.Extensions.Logging.Abstractions.NullLogger<SessionCommandProcessor>.Instance,
            durable: durable);

        const string workspace = "ws-1";
        const string session = "SHOW1";
        var seeded = GameReducer.Initial(session) with
        {
            Phase = Phase.Start,
            Choices = ["a", "b", "c"]
        };
        await durable.CommitAsync(
            workspace, session, Guid.NewGuid(), SessionSequence.None, seeded, [],
            DurableCommitPrecondition.SessionMustNotExist);
        game.Set(session, seeded);
        sessions.Touch(session, "performer", "p1");

        time.Advance(TimeSpan.FromSeconds(11));
        sessions.EvictIdleNow();
        Assert.False(game.TryGet(session, out _));

        var result = await processor.ApplyAsync(
            session,
            Actor.Verified(Role.Performer, "p1"),
            new OpenAnswers(30)
            {
                SessionCode = session,
                IssuedByRole = Role.Performer,
                IssuedById = "p1"
            },
            workspaceId: workspace);

        Assert.Equal(Outcome.Applied, result.Outcome);
        Assert.Equal(Phase.Guessing, result.State!.Phase);
        Assert.Equal(["a", "b", "c"], result.State.Choices);
        Assert.True(game.TryGet(session, out var hot));
        Assert.Equal(Phase.Guessing, hot.Phase);
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
