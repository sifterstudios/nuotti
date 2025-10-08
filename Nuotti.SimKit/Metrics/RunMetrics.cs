using System.Text.Json;
using System.Text.Json.Serialization;
namespace Nuotti.SimKit.Metrics;

public sealed class RunMetrics
{
    public DateTimeOffset StartedAtUtc { get; init; }
    public DateTimeOffset EndedAtUtc { get; init; }

    // Core acceptance criteria
    public double CommandApplyLatencyP50Ms { get; init; }
    public double CommandApplyLatencyP95Ms { get; init; }
    public int Disconnections { get; init; }
    public int Errors { get; init; }
    public double AnswerThroughputPerSec { get; init; }

    // Helpful counts
    public int CommandsIssued { get; init; }
    public int CommandsApplied { get; init; }
    public int AnswersSubmitted { get; init; }

    [JsonExtensionData]
    public Dictionary<string, object?>? Extra { get; init; }
}

public sealed class RunMetricsCollector
{
    private readonly object _gate = new();
    private readonly Dictionary<string, DateTimeOffset> _commandIssuedAt = new();
    private readonly List<double> _commandLatenciesMs = new();
    private int _disconnections;
    private int _errors;
    private int _answers;
    private readonly DateTimeOffset _startedAt;

    public RunMetricsCollector(DateTimeOffset? startedAtUtc = null)
    {
        _startedAt = startedAtUtc ?? DateTimeOffset.UtcNow;
    }

    public DateTimeOffset StartedAtUtc => _startedAt;

    public void RecordCommandIssued(string correlationId)
    {
        lock (_gate)
        {
            _commandIssuedAt[correlationId] = DateTimeOffset.UtcNow;
        }
    }

    public void RecordCommandApplied(string correlationId)
    {
        DateTimeOffset issued;
        lock (_gate)
        {
            if (!_commandIssuedAt.TryGetValue(correlationId, out issued)) return; // unknown
        }
        var now = DateTimeOffset.UtcNow;
        var ms = (now - issued).TotalMilliseconds;
        lock (_gate)
        {
            _commandLatenciesMs.Add(ms);
        }
    }

    public void RecordDisconnection()
    {
        Interlocked.Increment(ref _disconnections);
    }

    public void RecordError(Exception? _ = null)
    {
        Interlocked.Increment(ref _errors);
    }

    public void RecordAnswerSubmitted()
    {
        Interlocked.Increment(ref _answers);
    }

    public RunMetrics BuildSnapshot(DateTimeOffset? endedAtUtc = null)
    {
        double p50, p95;
        int issuedCount;
        lock (_gate)
        {
            issuedCount = _commandIssuedAt.Count;
            var arr = _commandLatenciesMs.ToArray();
            Array.Sort(arr);
            p50 = Percentile(arr, 50);
            p95 = Percentile(arr, 95);
        }
        var ended = endedAtUtc ?? DateTimeOffset.UtcNow;
        var elapsedSec = Math.Max((ended - _startedAt).TotalSeconds, 1e-6);
        var throughput = _answers / elapsedSec;
        return new RunMetrics
        {
            StartedAtUtc = _startedAt,
            EndedAtUtc = ended,
            CommandApplyLatencyP50Ms = p50,
            CommandApplyLatencyP95Ms = p95,
            Disconnections = _disconnections,
            Errors = _errors,
            AnswerThroughputPerSec = throughput,
            CommandsIssued = issuedCount,
            CommandsApplied = _commandLatenciesMs.Count,
            AnswersSubmitted = _answers
        };
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
}

public static class RunSummaryWriter
{
    public static (string Directory, string JsonPath, string MarkdownPath) Write(string? baseDir, RunMetrics metrics, DateTimeOffset? nowUtc = null)
    {
        var now = nowUtc ?? DateTimeOffset.UtcNow;
        var dir = Path.Combine(baseDir ?? Directory.GetCurrentDirectory(),
            "runs", now.ToString("yyyyMMdd-HHmm"));
        Directory.CreateDirectory(dir);

        var jsonPath = Path.Combine(dir, "report.json");
        var mdPath = Path.Combine(dir, "report.md");

        var json = JsonSerializer.Serialize(metrics, new JsonSerializerOptions
        {
            WriteIndented = true,
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        });
        File.WriteAllText(jsonPath, json);

        var md = BuildMarkdown(metrics);
        File.WriteAllText(mdPath, md);

        return (dir, jsonPath, mdPath);
    }

    private static string BuildMarkdown(RunMetrics m)
    {
        return string.Join(Environment.NewLine, new[]
        {
            "# Run Summary",
            $"Start: {m.StartedAtUtc:O}",
            $"End:   {m.EndedAtUtc:O}",
            "",
            "## Metrics",
            $"- Command→apply latency p50: {m.CommandApplyLatencyP50Ms:F1} ms",
            $"- Command→apply latency p95: {m.CommandApplyLatencyP95Ms:F1} ms",
            $"- Disconnections: {m.Disconnections}",
            $"- Errors: {m.Errors}",
            $"- Answer throughput: {m.AnswerThroughputPerSec:F3} answers/sec",
            "",
            "## Counters",
            $"- Commands issued: {m.CommandsIssued}",
            $"- Commands applied: {m.CommandsApplied}",
            $"- Answers submitted: {m.AnswersSubmitted}",
        });
    }
}