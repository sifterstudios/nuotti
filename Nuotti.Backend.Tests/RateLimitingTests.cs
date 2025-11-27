using Nuotti.Backend.RateLimiting;
using Xunit;
namespace Nuotti.Backend.Tests;

public class RateLimitingTests
{
    [Fact]
    public async Task Limiter_blocks_rapid_repeats_within_window()
    {
        var conn = Guid.NewGuid().ToString("N");
        var action = "SubmitAnswer";
        var window = TimeSpan.FromMilliseconds(500);

        // First call should be allowed
        Assert.True(ConnectionRateLimiter.TryAllow(conn, action, window));
        // Immediate repeat should be blocked
        Assert.False(ConnectionRateLimiter.TryAllow(conn, action, window));

        // After waiting for the window, should be allowed again
        await Task.Delay(window + TimeSpan.FromMilliseconds(50));
        Assert.True(ConnectionRateLimiter.TryAllow(conn, action, window));
    }

    [Fact]
    public void Different_actions_are_tracked_independently()
    {
        var conn = Guid.NewGuid().ToString("N");
        var a1 = "SubmitAnswer";
        var a2 = "PlayStop";
        var window = TimeSpan.FromSeconds(2);

        Assert.True(ConnectionRateLimiter.TryAllow(conn, a1, window));
        Assert.True(ConnectionRateLimiter.TryAllow(conn, a2, window));
        // Second call same action blocked
        Assert.False(ConnectionRateLimiter.TryAllow(conn, a1, window));
        // Other action still allowed once
        Assert.False(ConnectionRateLimiter.TryAllow(conn, a2, window));
    }

    [Fact]
    public void Different_connections_are_tracked_independently()
    {
        var conn1 = Guid.NewGuid().ToString("N");
        var conn2 = Guid.NewGuid().ToString("N");
        var action = "SubmitAnswer";
        var window = TimeSpan.FromMilliseconds(500);

        // Both connections can perform the action
        Assert.True(ConnectionRateLimiter.TryAllow(conn1, action, window));
        Assert.True(ConnectionRateLimiter.TryAllow(conn2, action, window));

        // Each connection is rate-limited independently
        Assert.False(ConnectionRateLimiter.TryAllow(conn1, action, window));
        Assert.False(ConnectionRateLimiter.TryAllow(conn2, action, window));
    }

    [Fact]
    public void First_call_after_window_expires_is_allowed()
    {
        var conn = Guid.NewGuid().ToString("N");
        var action = "SubmitAnswer";
        var window = TimeSpan.FromMilliseconds(100);

        // First call allowed
        Assert.True(ConnectionRateLimiter.TryAllow(conn, action, window));
        // Immediate repeat blocked
        Assert.False(ConnectionRateLimiter.TryAllow(conn, action, window));

        // Wait for window to expire
        Thread.Sleep(window + TimeSpan.FromMilliseconds(50));

        // Should be allowed again
        Assert.True(ConnectionRateLimiter.TryAllow(conn, action, window));
    }
}
