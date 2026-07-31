namespace Nuotti.Backend.Workspaces;

public static class WorkspaceHttpAccess
{
    public static async Task<WorkspacePrincipal?> AuthenticateAsync(
        HttpContext http, IWorkspaceAccessStore store, CancellationToken cancellationToken = default)
    {
        var authorization = http.Request.Headers.Authorization.ToString();
        const string prefix = "Bearer ";
        if (!authorization.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)) return null;
        var token = authorization[prefix.Length..].Trim();
        return token.Length == 0 ? null : await store.AuthenticateAsync(token, cancellationToken);
    }

    public static async Task<(WorkspacePrincipal? Principal, WorkspaceAccess? Access)> RequireSelectedAsync(
        HttpContext http,
        IWorkspaceAccessStore store,
        string workspaceId,
        CancellationToken cancellationToken = default)
    {
        var principal = await AuthenticateAsync(http, store, cancellationToken);
        if (principal is null || principal.SelectedWorkspaceId != workspaceId) return (principal, null);
        return (principal, await store.GetAccessAsync(principal, workspaceId, cancellationToken));
    }
}
