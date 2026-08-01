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
/// </summary>
public sealed class EntitlementGate
{
    readonly HashSet<(string WorkspaceId, EntitlementKind Kind)> _grants = new();

    public void Grant(string workspaceId, EntitlementKind kind)
        => _grants.Add((workspaceId, kind));

    public void Revoke(string workspaceId, EntitlementKind kind)
        => _grants.Remove((workspaceId, kind));

    public bool IsAllowed(string workspaceId, EntitlementKind kind)
        => _grants.Contains((workspaceId, kind));

    public void Ensure(string workspaceId, EntitlementKind kind)
    {
        if (!IsAllowed(workspaceId, kind))
            throw new UnauthorizedAccessException(
                $"Workspace '{workspaceId}' is not entitled for {kind}.");
    }
}
