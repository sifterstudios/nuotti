using Microsoft.Extensions.Options;
using Nuotti.Backend.Idempotency;
using Nuotti.Backend.Models;
using Xunit;

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
}
