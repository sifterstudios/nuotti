namespace Nuotti.Backend.Realtime;

/// <summary>
/// Authenticates an audience join token on an HTTP request.
/// </summary>
/// <remarks>
/// The audience read surfaces used to be Development-only for the same reason the hub was: they
/// identified a participant by an id in the query string, so any caller could read anybody's
/// answer or search a session they were never in. The join token fixes both - it names the
/// participant and the session it belongs to, and the caller cannot choose either.
/// </remarks>
internal static class AudienceHttpAccess
{
    public static async Task<AudienceParticipantIdentity?> AuthenticateAsync(
        HttpContext http, IAudienceJoinStore joins, string sessionCode, CancellationToken ct)
    {
        var authorization = http.Request.Headers.Authorization.ToString();
        const string prefix = "Bearer ";
        if (!authorization.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)) return null;
        var token = authorization[prefix.Length..].Trim();
        if (token.Length == 0) return null;

        var participant = await joins.AuthenticateAsync(token, ct);
        return participant is not null
            && string.Equals(participant.SessionCode, sessionCode, StringComparison.OrdinalIgnoreCase)
            ? participant
            : null;
    }
}
