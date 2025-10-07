using System.Collections.Concurrent;

namespace Nuotti.Backend.RateLimiting;

/// <summary>
/// Very lightweight per-connection rate limiter/debouncer keyed by an arbitrary action name.
/// Not distributed; in-memory only (per server instance).
/// </summary>
public static class ConnectionRateLimiter
{
    private static readonly ConcurrentDictionary<string, long> _lastTicks = new();

    /// <summary>
    /// Returns true if the action is allowed now, otherwise false if it should be debounced/rate-limited.
    /// </summary>
    /// <param name="connectionId">SignalR connection id.</param>
    /// <param name="actionKey">Action name, e.g. "SubmitAnswer" or "PlayStop".</param>
    /// <param name="window">Minimum interval between allowed actions.</param>
    public static bool TryAllow(string connectionId, string actionKey, TimeSpan window)
    {
        var key = MakeKey(connectionId, actionKey);
        var now = DateTimeOffset.UtcNow.Ticks;

        // If no previous record, store and allow
        var prev = _lastTicks.GetOrAdd(key, now);
        if (prev == now)
        {
            return true;
        }

        // If enough time has passed, update and allow
        var prevTime = new DateTimeOffset(prev, TimeSpan.Zero);
        if (now - prev >= window.Ticks)
        {
            _lastTicks[key] = now;
            return true;
        }

        // Too soon
        return false;
    }

    private static string MakeKey(string connectionId, string actionKey) => connectionId + "::" + actionKey;
}