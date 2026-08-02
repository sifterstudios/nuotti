namespace Nuotti.Contracts.V1.Governance;

public enum EntitlementKind
{
    DiagnosticsExport,
    AssetDownload,
    ShowAgentPairing,
    PublishPackage
}

/// <summary>
/// Workspace entitlement gate independent of role membership.
/// Launch kinds (asset download, Show Agent pairing, publish) are allowed by default so
/// durable workspaces keep working across API restarts; owners may revoke them explicitly.
/// Opt-in kinds such as diagnostics export still require an explicit grant.
/// </summary>
public sealed class EntitlementGate
{
    readonly HashSet<(string WorkspaceId, EntitlementKind Kind)> _grants = new();
    readonly HashSet<(string WorkspaceId, EntitlementKind Kind)> _revokes = new();

    public static bool IsLaunchDefault(EntitlementKind kind) =>
        kind is EntitlementKind.AssetDownload
            or EntitlementKind.ShowAgentPairing
            or EntitlementKind.PublishPackage;

    public void Grant(string workspaceId, EntitlementKind kind)
    {
        _revokes.Remove((workspaceId, kind));
        _grants.Add((workspaceId, kind));
    }

    public void Revoke(string workspaceId, EntitlementKind kind)
    {
        _grants.Remove((workspaceId, kind));
        _revokes.Add((workspaceId, kind));
    }

    public bool IsAllowed(string workspaceId, EntitlementKind kind)
    {
        if (_revokes.Contains((workspaceId, kind))) return false;
        if (IsLaunchDefault(kind)) return true;
        return _grants.Contains((workspaceId, kind));
    }

    public void Ensure(string workspaceId, EntitlementKind kind)
    {
        if (!IsAllowed(workspaceId, kind))
            throw new UnauthorizedAccessException(
                $"Workspace '{workspaceId}' is not entitled for {kind}.");
    }
}
