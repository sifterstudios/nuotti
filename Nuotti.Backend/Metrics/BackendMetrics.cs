using System.Collections.Concurrent;
using System.Text.Json;
using Nuotti.Backend.Sessions;

namespace Nuotti.Backend.Metrics;

/// <summary>
/// Backend metrics collection for observability.
/// Tracks: active connections per role, answers per minute, command→apply latency.
/// </summary>
public sealed class BackendMetrics
{
    private readonly object _lock = new();
    private readonly DateTimeOffset _startedAtUtc = DateTimeOffset.UtcNow;
    private readonly ConcurrentDictionary<Guid, DateTimeOffset> _commandStartTimes = new();
    private readonly List<double> _commandLatenciesMs = new();
    private int _totalAnswersSubmitted;
    private DateTimeOffset _lastAnswerTime = DateTimeOffset.MinValue;
    
    public DateTimeOffset StartedAtUtc => _startedAtUtc;
    public TimeSpan Uptime => DateTimeOffset.UtcNow - _startedAtUtc;

    /// <summary>
    /// Records that a command was received (start of latency measurement).
    /// </summary>
    public void RecordCommandReceived(Guid commandId)
    {
        _commandStartTimes[commandId] = DateTimeOffset.UtcNow;
    }

    /// <summary>
    /// Records that a command was applied (end of latency measurement).
    /// </summary>
    public void RecordCommandApplied(Guid commandId)
    {
        if (_commandStartTimes.TryRemove(commandId, out var startTime))
        {
            var latencyMs = (DateTimeOffset.UtcNow - startTime).TotalMilliseconds;
            lock (_lock)
            {
                _commandLatenciesMs.Add(latencyMs);
                // Keep only last 1000 measurements to avoid unbounded growth
                if (_commandLatenciesMs.Count > 1000)
                {
                    _commandLatenciesMs.RemoveAt(0);
                }
            }
        }
    }

    /// <summary>
    /// Records an answer submission.
    /// </summary>
    public void RecordAnswerSubmitted()
    {
        Interlocked.Increment(ref _totalAnswersSubmitted);
        _lastAnswerTime = DateTimeOffset.UtcNow;
    }

    /// <summary>
    /// Gets answers per minute rate.
    /// </summary>
    public double GetAnswersPerMinute()
    {
        var uptimeMinutes = Math.Max(1, Uptime.TotalMinutes);
        return _totalAnswersSubmitted / uptimeMinutes;
    }

    /// <summary>
    /// Calculates latency percentiles from recent command latencies.
    /// </summary>
    public (double p50, double p95) GetLatencyPercentiles()
    {
        double[] sorted;
        lock (_lock)
        {
            if (_commandLatenciesMs.Count == 0)
            {
                return (0, 0);
            }
            sorted = _commandLatenciesMs.ToArray();
        }
        
        Array.Sort(sorted);
        var p50 = Percentile(sorted, 50);
        var p95 = Percentile(sorted, 95);
        return (p50, p95);
    }

    private static double Percentile(double[] sorted, double p)
    {
        if (sorted.Length == 0) return 0;
        var rank = (p / 100.0) * (sorted.Length - 1);
        var low = (int)Math.Floor(rank);
        var high = (int)Math.Ceiling(rank);
        if (low == high) return sorted[low];
        var weight = rank - low;
        return sorted[low] * (1 - weight) + sorted[high] * weight;
    }

    /// <summary>
    /// Gets active connections per role from session store.
    /// </summary>
    public Dictionary<string, int> GetActiveConnectionsPerRole(ISessionStore sessionStore)
    {
        var counts = sessionStore.GetAggregateCounts();
        return new Dictionary<string, int>
        {
            { "performer", counts.Performer },
            { "projector", counts.Projector },
            { "engine", counts.Engine },
            { "audience", counts.Audiences }
        };
    }

    /// <summary>
    /// Creates a snapshot of all metrics.
    /// </summary>
    public MetricsSnapshot Snapshot(ISessionStore? sessionStore = null)
    {
        var (p50, p95) = GetLatencyPercentiles();
        var connections = sessionStore != null ? GetActiveConnectionsPerRole(sessionStore) : new Dictionary<string, int>();
        
        return new MetricsSnapshot(
            UptimeSeconds: Math.Max(0, Uptime.TotalSeconds),
            AnswersPerMinute: GetAnswersPerMinute(),
            CommandApplyLatencyP50Ms: p50,
            CommandApplyLatencyP95Ms: p95,
            TotalAnswersSubmitted: _totalAnswersSubmitted,
            ActiveConnections: connections
        );
    }

    /// <summary>
    /// Serializes metrics to JSON.
    /// </summary>
    public string ToJson(ISessionStore? sessionStore = null)
    {
        var snap = Snapshot(sessionStore);
        return JsonSerializer.Serialize(snap, new JsonSerializerOptions 
        { 
            WriteIndented = false, 
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase 
        });
    }
}

/// <summary>
/// Snapshot of backend metrics.
/// </summary>
public sealed record MetricsSnapshot(
    double UptimeSeconds,
    double AnswersPerMinute,
    double CommandApplyLatencyP50Ms,
    double CommandApplyLatencyP95Ms,
    int TotalAnswersSubmitted,
    Dictionary<string, int> ActiveConnections
);

