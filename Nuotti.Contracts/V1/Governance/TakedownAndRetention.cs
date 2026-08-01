namespace Nuotti.Contracts.V1.Governance;

public enum TakedownStatus
{
    Open,
    Enforced,
    Released
}

public sealed record TakedownCase(
    string CaseId,
    string WorkspaceId,
    string AssetRevisionId,
    string Reason,
    TakedownStatus Status,
    DateTimeOffset OpenedAtUtc,
    DateTimeOffset? EnforcedAtUtc);

/// <summary>
/// Provenance/takedown seam: enforced cases block downloads and Show Agent grants for an asset.
/// </summary>
public sealed class TakedownCaseStore
{
    readonly Dictionary<string, TakedownCase> _cases = new(StringComparer.Ordinal);
    readonly object _gate = new();

    public TakedownCase Open(string workspaceId, string assetRevisionId, string reason, DateTimeOffset nowUtc)
    {
        lock (_gate)
        {
            var id = $"td_{Guid.NewGuid():N}";
            var opened = new TakedownCase(id, workspaceId, assetRevisionId, reason, TakedownStatus.Open, nowUtc, null);
            _cases[id] = opened;
            return opened;
        }
    }

    public TakedownCase Enforce(string caseId, DateTimeOffset nowUtc)
    {
        lock (_gate)
        {
            if (!_cases.TryGetValue(caseId, out var existing))
                throw new KeyNotFoundException(caseId);
            var enforced = existing with { Status = TakedownStatus.Enforced, EnforcedAtUtc = nowUtc };
            _cases[caseId] = enforced;
            return enforced;
        }
    }

    public bool IsBlocked(string workspaceId, string assetRevisionId)
    {
        lock (_gate)
        {
            return _cases.Values.Any(c =>
                c.Status == TakedownStatus.Enforced
                && c.WorkspaceId == workspaceId
                && c.AssetRevisionId == assetRevisionId);
        }
    }
}

/// <summary>
/// Retention boundary helper for datasets that must leave after a TTL.
/// </summary>
public static class RetentionBoundary
{
    public static readonly TimeSpan SessionResults = TimeSpan.FromDays(30);
    public static readonly TimeSpan AuditLogs = TimeSpan.FromDays(30);
    public static readonly TimeSpan SupportBundles = TimeSpan.FromDays(7);

    public static bool IsExpired(DateTimeOffset capturedAtUtc, TimeSpan retention, DateTimeOffset nowUtc)
        => nowUtc - capturedAtUtc >= retention;
}
