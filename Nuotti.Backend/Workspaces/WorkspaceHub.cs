using Microsoft.AspNetCore.SignalR;

namespace Nuotti.Backend.Workspaces;

/// <summary>Read-only realtime transport scoped by an authenticated Workspace selection.</summary>
public sealed class WorkspaceHub(IWorkspaceAccessStore accessStore) : Hub
{
    public override async Task OnConnectedAsync()
    {
        var http = Context.GetHttpContext();
        var workspaceId = http?.Request.Query["workspaceId"].ToString();
        var sessionCode = http?.Request.Query["sessionCode"].ToString();
        var token = http?.Request.Query["access_token"].ToString();
        if (string.IsNullOrWhiteSpace(token))
        {
            var authorization = http?.Request.Headers.Authorization.ToString();
            const string prefix = "Bearer ";
            token = authorization?.StartsWith(prefix, StringComparison.OrdinalIgnoreCase) == true
                ? authorization[prefix.Length..].Trim() : null;
        }

        var principal = string.IsNullOrWhiteSpace(token) ? null : await accessStore.AuthenticateAsync(token);
        if (principal is null || string.IsNullOrWhiteSpace(workspaceId) || string.IsNullOrWhiteSpace(sessionCode)
            || principal.SelectedWorkspaceId != workspaceId
            || await accessStore.GetAccessAsync(principal, workspaceId) is null)
        {
            Context.Abort();
            return;
        }

        await Groups.AddToGroupAsync(Context.ConnectionId, GroupName(workspaceId, sessionCode));
        await base.OnConnectedAsync();
    }

    public static string GroupName(string workspaceId, string sessionCode) => $"{workspaceId}:{sessionCode}";
}
