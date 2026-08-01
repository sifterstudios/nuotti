using System.Text;
using System.Text.Json;
using Nuotti.Contracts.V1.Qualification;

namespace Nuotti.SimKit.Trace;

public sealed record ScheduleAction(int Tick, string Actor, string Kind, string? Detail = null);

/// <summary>
/// Seeded schedule generator for PR/nightly qualification runs.
/// </summary>
public sealed class ScheduleGenerator(int seed)
{
    readonly Random _rng = new(seed);

    public IReadOnlyList<ScheduleAction> Generate(int actionCount, int audiences = 50)
    {
        if (actionCount <= 0) throw new ArgumentOutOfRangeException(nameof(actionCount));
        if (audiences <= 0) throw new ArgumentOutOfRangeException(nameof(audiences));

        var actions = new List<ScheduleAction>(actionCount);
        var kinds = new[] { "Join", "SubmitAnswer", "Reconnect", "Lock", "Reveal" };
        for (var i = 0; i < actionCount; i++)
        {
            var kind = kinds[_rng.Next(kinds.Length)];
            var actor = $"A{_rng.Next(audiences):D3}";
            actions.Add(new ScheduleAction(i, actor, kind, Detail: $"seed={seed};i={i}"));
        }
        return actions;
    }
}

public sealed record TraceEvent(long Sequence, DateTimeOffset AtUtc, string Kind, string Detail);

/// <summary>
/// Bounded JSONL sink that can emit a minimized replay trace on failure.
/// </summary>
public sealed class JsonlTraceSink(int capacity = LoadThresholds.MinimizedTraceEventCap)
{
    readonly Queue<TraceEvent> _events = new();
    readonly object _gate = new();
    long _sequence;

    public int Count
    {
        get { lock (_gate) return _events.Count; }
    }

    public void Record(string kind, string detail, DateTimeOffset? atUtc = null)
    {
        lock (_gate)
        {
            _sequence++;
            _events.Enqueue(new TraceEvent(_sequence, atUtc ?? DateTimeOffset.UtcNow, kind, detail));
            while (_events.Count > capacity)
                _events.Dequeue();
        }
    }

    public string RenderMinimizedReplay()
    {
        lock (_gate)
        {
            var sb = new StringBuilder();
            sb.AppendLine($"# minimized-replay events={_events.Count} cap={capacity}");
            foreach (var evt in _events)
                sb.AppendLine(JsonSerializer.Serialize(evt));
            return sb.ToString();
        }
    }

    public void WriteMinimizedReplay(string path)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(path))!);
        File.WriteAllText(path, RenderMinimizedReplay());
    }
}
