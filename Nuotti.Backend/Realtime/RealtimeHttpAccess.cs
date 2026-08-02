namespace Nuotti.Backend.Realtime;

/// <summary>
/// Resolves the same <see cref="ConnectionPrincipal"/> the hub does, for an HTTP request.
/// </summary>
/// <remarks>
/// A session's read surfaces are wanted by all three kinds of caller: a phone fetching the state it
/// just joined, a venue device catching up after a reconnect, and a member watching from the
/// Performer app. Each already carries a credential that names its session, so rather than invent a
/// third authorization scheme these routes ask the resolver the hub asks. That is also what keeps
/// them honest: no principal, no read - where before they were simply unmapped outside Development
/// and therefore worked for nobody.
/// </remarks>
internal static class RealtimeHttpAccess
{
    public static Task<ConnectionPrincipal?> ResolveAsync(
        HttpContext http, IConnectionPrincipalResolver resolver, string sessionCode,
        CancellationToken cancellationToken = default)
    {
        var authorization = http.Request.Headers.Authorization.ToString();
        const string prefix = "Bearer ";
        var token = authorization.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)
            ? authorization[prefix.Length..].Trim()
            : null;

        // A workspace member's token names no session, so the route's own session code is what
        // scopes them. Audience tickets and device leases name their own and would refuse a
        // mismatch, which is exactly the check wanted here.
        return resolver.ResolveAsync(new RealtimeConnectionRequest(
            token, sessionCode, http.Request.Query["workspaceId"].ToString(), null), cancellationToken);
    }
}
