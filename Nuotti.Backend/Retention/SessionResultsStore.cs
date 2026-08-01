namespace Nuotti.Backend.Retention;

/// <summary>
/// Retained, reproducible Session results after EndGame. Identifiable scores are kept for 30 days.
/// </summary>
public sealed record SessionShowResult(
    string WorkspaceId,
    string SessionCode,
    DateTimeOffset CompletedAtUtc,
    IReadOnlyDictionary<string, int> Scores,
    long LastSequence,
    Guid CausingCommandId,
    int SongCount);

public interface ISessionResultsStore
{
    static readonly TimeSpan Retention = TimeSpan.FromDays(30);

    Task SaveAsync(SessionShowResult result, CancellationToken cancellationToken = default);
    Task<SessionShowResult?> GetAsync(string workspaceId, string sessionCode, CancellationToken cancellationToken = default);
    Task<int> PruneExpiredAsync(DateTimeOffset nowUtc, CancellationToken cancellationToken = default);
}

public sealed class InMemorySessionResultsStore : ISessionResultsStore
{
    readonly Dictionary<(string Workspace, string Session), SessionShowResult> _results = new();
    readonly object _gate = new();

    public Task SaveAsync(SessionShowResult result, CancellationToken cancellationToken = default)
    {
        lock (_gate) _results[(result.WorkspaceId, result.SessionCode)] = result;
        return Task.CompletedTask;
    }

    public Task<SessionShowResult?> GetAsync(string workspaceId, string sessionCode, CancellationToken cancellationToken = default)
    {
        lock (_gate)
            return Task.FromResult(_results.TryGetValue((workspaceId, sessionCode), out var r) ? r : null);
    }

    public Task<int> PruneExpiredAsync(DateTimeOffset nowUtc, CancellationToken cancellationToken = default)
    {
        lock (_gate)
        {
            var cutoff = nowUtc - ISessionResultsStore.Retention;
            var expired = _results.Where(kv => kv.Value.CompletedAtUtc < cutoff).Select(kv => kv.Key).ToList();
            foreach (var key in expired) _results.Remove(key);
            return Task.FromResult(expired.Count);
        }
    }
}
