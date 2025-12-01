using Microsoft.Extensions.Options;
using Nuotti.Backend.Idempotency;
using Nuotti.Backend.Models;

namespace Nuotti.Backend.Tests;

public class IdempotencyStoreTests
{
    static InMemoryIdempotencyStore Create(FakeTimeProvider time, int ttlSeconds = 60, int maxPerSession = 8)
    {
        var options = Options.Create(new NuottiOptions
        {
            IdempotencyTtlSeconds = ttlSeconds,
            IdempotencyMaxPerSession = maxPerSession
        });
        return new InMemoryIdempotencyStore(options, time);
    }

    [Fact]
    public void First_Register_Is_New_Second_Is_Duplicate()
    {
        var time = new FakeTimeProvider(DateTimeOffset.UtcNow);
        var store = Create(time);
        var session = "s1";
        var cmd = Guid.Parse("aaaaaaaa-aaaa-4aaa-aaaa-aaaaaaaaaaaa");

        var first = store.TryRegister(session, cmd);
        var second = store.TryRegister(session, cmd);

        Assert.True(first);
        Assert.False(second);
    }

    [Fact]
    public void Duplicate_After_TTL_Expires_Is_Treated_As_New()
    {
        var now = DateTimeOffset.Parse("2025-01-01T00:00:00Z");
        var time = new FakeTimeProvider(now);
        var store = Create(time, ttlSeconds: 5);
        var session = "game-42";
        var cmd = Guid.Parse("bbbbbbbb-bbbb-4bbb-bbbb-bbbbbbbbbbbb");

        Assert.True(store.TryRegister(session, cmd));
        Assert.False(store.TryRegister(session, cmd));

        // advance beyond TTL
        time.Advance(TimeSpan.FromSeconds(6));

        // same command id should be treated as new after expiration
        Assert.True(store.TryRegister(session, cmd));
    }

    [Fact]
    public void Capacity_Limit_Drops_Old_Ids()
    {
        var time = new FakeTimeProvider(DateTimeOffset.UtcNow);
        var store = Create(time, ttlSeconds: 60, maxPerSession: 2);
        var session = "cap";
        var a = Guid.Parse("11111111-1111-4111-8111-111111111111");
        var b = Guid.Parse("22222222-2222-4222-8222-222222222222");
        var c = Guid.Parse("33333333-3333-4333-8333-333333333333");

        Assert.True(store.TryRegister(session, a));
        Assert.True(store.TryRegister(session, b));
        // adding third should evict first (a)
        Assert.True(store.TryRegister(session, c));

        // a should no longer be considered duplicate
        Assert.True(store.TryRegister(session, a));
    }

    [Fact]
    public void Duplicate_CommandId_within_TTL_returns_false_no_op()
    {
        var time = new FakeTimeProvider(DateTimeOffset.UtcNow);
        var store = Create(time, ttlSeconds: 60);
        var session = "test-session";
        var commandId = Guid.Parse("aaaaaaaa-aaaa-4aaa-aaaa-aaaaaaaaaaaa");

        // First registration succeeds
        var first = store.TryRegister(session, commandId);
        Assert.True(first);

        // Second registration within TTL returns false (duplicate, no-op)
        var second = store.TryRegister(session, commandId);
        Assert.False(second);

        // Third registration also returns false
        var third = store.TryRegister(session, commandId);
        Assert.False(third);
    }

    [Fact]
    public void Duplicate_CommandId_after_TTL_expires_returns_true()
    {
        var now = DateTimeOffset.Parse("2025-01-01T00:00:00Z");
        var time = new FakeTimeProvider(now);
        var store = Create(time, ttlSeconds: 10);
        var session = "test-session";
        var commandId = Guid.Parse("bbbbbbbb-bbbb-4bbb-bbbb-bbbbbbbbbbbb");

        // First registration
        Assert.True(store.TryRegister(session, commandId));

        // Duplicate within TTL
        Assert.False(store.TryRegister(session, commandId));

        // Advance time beyond TTL
        time.Advance(TimeSpan.FromSeconds(11));

        // After TTL, same command ID is treated as new
        Assert.True(store.TryRegister(session, commandId));
    }

    [Fact]
    public void Different_CommandIds_in_same_session_all_succeed()
    {
        var time = new FakeTimeProvider(DateTimeOffset.UtcNow);
        var store = Create(time);
        var session = "test-session";
        var cmd1 = Guid.Parse("11111111-1111-4111-8111-111111111111");
        var cmd2 = Guid.Parse("22222222-2222-4222-8222-222222222222");
        var cmd3 = Guid.Parse("33333333-3333-4333-8333-333333333333");

        // All different command IDs should succeed
        Assert.True(store.TryRegister(session, cmd1));
        Assert.True(store.TryRegister(session, cmd2));
        Assert.True(store.TryRegister(session, cmd3));

        // Duplicates of each should fail
        Assert.False(store.TryRegister(session, cmd1));
        Assert.False(store.TryRegister(session, cmd2));
        Assert.False(store.TryRegister(session, cmd3));
    }
}
