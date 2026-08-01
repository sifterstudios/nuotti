namespace Nuotti.Backend.Workspaces;

/// <summary>Fixed Development testing workspace used by Performer without paste auth.</summary>
public sealed record DevelopmentWorkspaceFixture(
    string WorkspaceId,
    string WorkspaceName,
    string Email,
    string SessionToken);

public static class DevelopmentWorkspaceDefaults
{
    public const string WorkspaceId = "ws_dev_local";
    public const string WorkspaceName = "Local testing";
    public const string Email = "dev@nuotti.local";
    public const string SessionToken = "dev-session-token";
    public const string UserId = "usr_dev_local";
}
