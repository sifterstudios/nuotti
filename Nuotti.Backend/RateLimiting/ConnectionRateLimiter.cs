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

        // TryAdd, not GetOrAdd-and-compare. Comparing the stored tick to `now` to detect a first
        // call is wrong: two calls close enough together read the same tick value, so the second
        // one looked like a first call and was let through the limit. TryAdd answers "was there a
        // previous record" directly, independent of clock granularity.
        if (_lastTicks.TryAdd(key, now))
        {
            return true;
        }

        while (true)
        {
            if (!_lastTicks.TryGetValue(key, out var prev))
            {
                // Evicted between the TryAdd and here; treat this as a first call.
                return _lastTicks.TryAdd(key, now);
            }

            if (now - prev < window.Ticks)
            {
                // Too soon
                return false;
            }

            // Only the caller that wins the update is allowed through, so concurrent callers on the
            // same connection cannot both pass one window.
            if (_lastTicks.TryUpdate(key, now, prev))
            {
                return true;
            }
        }
    }

    private static string MakeKey(string connectionId, string actionKey) => connectionId + "::" + actionKey;
}