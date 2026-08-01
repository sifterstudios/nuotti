namespace Nuotti.Backend.Sessions;

/// <summary>
/// Remembers which Workspace owns a live Session code so phase and relay endpoints can
/// publish WorkspacePublication (Show Agent fan-out) and commit to the durable store
/// under the same key CreateSession used.
/// </summary>
public interface ISessionWorkspaceBinder
{
    void Bind(string sessionCode, string workspaceId);
    string? Resolve(string sessionCode);
}

public sealed class InMemorySessionWorkspaceBinder : ISessionWorkspaceBinder
{
    readonly Dictionary<string, string> _map = new(StringComparer.OrdinalIgnoreCase);
    readonly object _gate = new();

    public void Bind(string sessionCode, string workspaceId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sessionCode);
        ArgumentException.ThrowIfNullOrWhiteSpace(workspaceId);
        lock (_gate) _map[sessionCode.Trim()] = workspaceId.Trim();
    }

    public string? Resolve(string sessionCode)
    {
        if (string.IsNullOrWhiteSpace(sessionCode)) return null;
        lock (_gate) return _map.TryGetValue(sessionCode.Trim(), out var workspaceId) ? workspaceId : null;
    }
}
