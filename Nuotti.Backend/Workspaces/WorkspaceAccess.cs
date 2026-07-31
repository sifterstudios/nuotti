using System.Text.Json.Serialization;

namespace Nuotti.Backend.Workspaces;

[JsonConverter(typeof(JsonStringEnumConverter<WorkspaceRole>))]
public enum WorkspaceRole { Owner, Member }

public sealed record WorkspacePrincipal(
    string UserId,
    string Email,
    string? SelectedWorkspaceId,
    [property: JsonIgnore] string? AuthenticationSessionId = null);
public sealed record WorkspaceAccess(string WorkspaceId, string Name, WorkspaceRole Role);
public sealed record WorkspaceMember(string UserId, string Email, WorkspaceRole Role);
public sealed record IssuedMagicLink(string Token, DateTimeOffset ExpiresAt);
public sealed record RedeemedMagicLink(string SessionToken, WorkspacePrincipal Principal);

public interface IWorkspaceAccessStore
{
    Task<IssuedMagicLink> IssueSignInAsync(string email, CancellationToken cancellationToken = default);
    Task<RedeemedMagicLink?> RedeemAsync(string token, CancellationToken cancellationToken = default);
    Task<WorkspacePrincipal?> AuthenticateAsync(string sessionToken, CancellationToken cancellationToken = default);
    Task<WorkspaceAccess> CreateWorkspaceAsync(WorkspacePrincipal principal, string name, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<WorkspaceAccess>> ListAsync(WorkspacePrincipal principal, CancellationToken cancellationToken = default);
    Task<WorkspacePrincipal?> SelectAsync(WorkspacePrincipal principal, string workspaceId, CancellationToken cancellationToken = default);
    Task<WorkspaceAccess?> GetAccessAsync(WorkspacePrincipal principal, string workspaceId, CancellationToken cancellationToken = default);
    Task<IssuedMagicLink?> InviteAsync(WorkspacePrincipal owner, string workspaceId, string email, CancellationToken cancellationToken = default);
    Task<bool> RevokeAsync(WorkspacePrincipal owner, string workspaceId, string memberUserId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<WorkspaceMember>?> MembersAsync(WorkspacePrincipal principal, string workspaceId, CancellationToken cancellationToken = default);
}

internal static class WorkspaceTokens
{
    internal static string New() => Convert.ToBase64String(System.Security.Cryptography.RandomNumberGenerator.GetBytes(32))
        .Replace('+', '-').Replace('/', '_').TrimEnd('=');

    internal static string Hash(string value) => Convert.ToHexString(
        System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(value)));

    internal static string NormalizeEmail(string email) => email.Trim().ToLowerInvariant();
}
