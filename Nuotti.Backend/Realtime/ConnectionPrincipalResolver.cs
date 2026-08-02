using Nuotti.Backend.ShowAgents;
using Nuotti.Backend.Workspaces;
using Nuotti.Contracts.V1.Enum;

namespace Nuotti.Backend.Realtime;

/// <summary>
/// Turns the credential on an incoming realtime connection into a <see cref="ConnectionPrincipal"/>.
/// </summary>
public interface IConnectionPrincipalResolver
{
    Task<ConnectionPrincipal?> ResolveAsync(RealtimeConnectionRequest request, CancellationToken cancellationToken = default);
}

/// <summary>The credential and routing information a connection presents, transport-agnostic.</summary>
public sealed record RealtimeConnectionRequest(
    string? AccessToken,
    string? SessionCode,
    string? WorkspaceId,
    string? DeviceRole);

/// <summary>
/// Tries each credential kind in turn. A connection presents exactly one, so the first store that
/// recognises the token decides the principal.
/// </summary>
/// <remarks>
/// Order matters only for cost, not correctness: the token formats are disjoint because each store
/// hashes and namespaces its own. An unrecognised or absent token resolves to null, and the caller
/// aborts the connection - there is deliberately no anonymous fallback.
/// </remarks>
public sealed class ConnectionPrincipalResolver(
    IWorkspaceAccessStore workspaces,
    IShowAgentAccessStore devices,
    IAudienceJoinStore audience) : IConnectionPrincipalResolver
{
    public async Task<ConnectionPrincipal?> ResolveAsync(
        RealtimeConnectionRequest request, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(request.AccessToken) || string.IsNullOrWhiteSpace(request.SessionCode))
            return null;

        var token = request.AccessToken.Trim();
        var sessionCode = request.SessionCode.Trim();

        // 1. Audience is the highest-volume case and the cheapest lookup, so it goes first.
        var participant = await audience.AuthenticateAsync(token, cancellationToken);
        if (participant is not null)
            return string.Equals(participant.SessionCode, sessionCode, StringComparison.OrdinalIgnoreCase)
                ? ConnectionPrincipal.ForAudience(participant.ParticipantId, participant.SessionCode)
                : null;

        // 2. A paired venue device. Its lease already names the workspace and session it belongs
        //    to, so a device cannot follow a code into somebody else's show.
        var lease = await devices.AuthenticateAsync(token, cancellationToken);
        if (lease is not null)
        {
            if (!string.Equals(lease.SessionCode, sessionCode, StringComparison.OrdinalIgnoreCase)) return null;
            var role = string.Equals(request.DeviceRole, "projector", StringComparison.OrdinalIgnoreCase)
                ? Role.Projector
                : Role.Engine;
            return ConnectionPrincipal.ForVenueDevice(lease.AgentId, lease.WorkspaceId, lease.SessionCode, role);
        }

        // 3. A signed-in workspace member. Membership alone is not enough: the principal must have
        //    this workspace selected, matching how every workspace-scoped HTTP route behaves.
        var user = await workspaces.AuthenticateAsync(token, cancellationToken);
        if (user is null || string.IsNullOrWhiteSpace(request.WorkspaceId)) return null;
        if (!string.Equals(user.SelectedWorkspaceId, request.WorkspaceId, StringComparison.Ordinal)) return null;
        if (await workspaces.GetAccessAsync(user, request.WorkspaceId, cancellationToken) is null) return null;

        return ConnectionPrincipal.ForWorkspaceUser(user.UserId, request.WorkspaceId, sessionCode);
    }
}
