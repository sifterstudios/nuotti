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
        if (string.IsNullOrWhiteSpace(request.AccessToken)) return null;

        var token = request.AccessToken.Trim();

        // The session code on the request is a claim, not a credential. Audience tickets and device
        // leases each name their own session, so the caller need not supply one at all - and a
        // supplied one that disagrees is refused rather than believed. This is what lets a venue
        // device be paired while it is running: it does not have to know which show it is joining
        // before it connects.
        var claimedSession = request.SessionCode?.Trim();

        // 1. Audience is the highest-volume case and the cheapest lookup, so it goes first.
        var participant = await audience.AuthenticateAsync(token, cancellationToken);
        if (participant is not null)
            return Contradicts(claimedSession, participant.SessionCode)
                ? null
                : ConnectionPrincipal.ForAudience(participant.ParticipantId, participant.SessionCode);

        // 2. A paired venue device. Its lease already names the workspace and session it belongs
        //    to, so a device cannot follow a code into somebody else's show.
        var lease = await devices.AuthenticateAsync(token, cancellationToken);
        if (lease is not null)
        {
            if (Contradicts(claimedSession, lease.SessionCode)) return null;
            var role = string.Equals(request.DeviceRole, "projector", StringComparison.OrdinalIgnoreCase)
                ? Role.Projector
                : Role.Engine;
            return ConnectionPrincipal.ForVenueDevice(lease.AgentId, lease.WorkspaceId, lease.SessionCode, role);
        }

        // 3. A signed-in workspace member. Their token names no session - a member runs many - so
        //    this is the one principal that must say which one. Membership alone is not enough
        //    either: the workspace must be selected, matching every workspace-scoped HTTP route.
        var user = await workspaces.AuthenticateAsync(token, cancellationToken);
        if (user is null || string.IsNullOrWhiteSpace(claimedSession)) return null;
        if (string.IsNullOrWhiteSpace(request.WorkspaceId)) return null;
        if (!string.Equals(user.SelectedWorkspaceId, request.WorkspaceId, StringComparison.Ordinal)) return null;
        if (await workspaces.GetAccessAsync(user, request.WorkspaceId, cancellationToken) is null) return null;

        return ConnectionPrincipal.ForWorkspaceUser(user.UserId, request.WorkspaceId, claimedSession);
    }

    /// <summary>
    /// True when the caller named a session and it is not the one its credential belongs to.
    /// Naming none is allowed; naming the wrong one is not.
    /// </summary>
    static bool Contradicts(string? claimed, string actual)
        => !string.IsNullOrWhiteSpace(claimed)
            && !string.Equals(claimed, actual, StringComparison.OrdinalIgnoreCase);
}
