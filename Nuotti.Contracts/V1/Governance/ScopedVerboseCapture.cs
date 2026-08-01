namespace Nuotti.Contracts.V1.Governance;

public enum DiagnosticVerbosity
{
    Information = 0,
    Debug = 1,
    Verbose = 2
}

/// <summary>
/// Scoped Debug/Verbose capture that auto-expires. Outside the window, level falls back to Information.
/// </summary>
public sealed class ScopedVerboseCapture
{
    readonly object _gate = new();
    DiagnosticVerbosity _baseline = DiagnosticVerbosity.Information;
    DiagnosticVerbosity _elevated = DiagnosticVerbosity.Information;
    DateTimeOffset? _expiresAt;
    string? _scopeId;

    public DiagnosticVerbosity EffectiveLevel(DateTimeOffset nowUtc)
    {
        lock (_gate)
        {
            if (_expiresAt is { } expires && nowUtc >= expires)
            {
                _elevated = _baseline;
                _expiresAt = null;
                _scopeId = null;
            }
            return _elevated;
        }
    }

    public string? ActiveScopeId(DateTimeOffset nowUtc)
    {
        lock (_gate)
        {
            _ = EffectiveLevel(nowUtc);
            return _scopeId;
        }
    }

    public string Elevate(DiagnosticVerbosity level, TimeSpan ttl, DateTimeOffset nowUtc, string? scopeId = null)
    {
        if (level < DiagnosticVerbosity.Debug)
            throw new ArgumentOutOfRangeException(nameof(level), "Elevation must be Debug or Verbose.");
        if (ttl <= TimeSpan.Zero || ttl > TimeSpan.FromHours(2))
            throw new ArgumentOutOfRangeException(nameof(ttl), "TTL must be between 0 and 2 hours.");

        lock (_gate)
        {
            _elevated = level;
            _expiresAt = nowUtc.Add(ttl);
            _scopeId = scopeId ?? $"diag_{Guid.NewGuid():N}"[..16];
            return _scopeId;
        }
    }

    public void Clear(DateTimeOffset nowUtc)
    {
        lock (_gate)
        {
            _elevated = _baseline;
            _expiresAt = null;
            _scopeId = null;
            _ = nowUtc;
        }
    }
}
