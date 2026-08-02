using Nuotti.Backend.Realtime;
using Nuotti.Backend.Sessions;

namespace Nuotti.Backend.Endpoints;

/// <summary>
/// The one deliberately public write surface: an anonymous phone exchanging a session code for a
/// scoped credential.
/// </summary>
/// <remarks>
/// Everything else a client can do requires a token; this is where an audience member gets one.
/// It is rate limited per IP because the session code is short and therefore guessable, and it
/// refuses codes for sessions that do not exist so it cannot be used to enumerate them.
/// </remarks>
public static class AudienceJoinEndpoints
{
    public sealed record JoinRequest(string DeviceSecret, string? DisplayName = null);

    public sealed record JoinResponse(string ParticipantId, string SessionCode, string Token, DateTimeOffset ExpiresAt);

    public static void MapAudienceJoinEndpoints(this WebApplication app)
    {
        app.MapPost("/v1/sessions/{sessionCode}/join", async (
            string sessionCode,
            JoinRequest request,
            IAudienceJoinStore joins,
            ISessionWorkspaceBinder sessions,
            CancellationToken ct) =>
        {
            if (string.IsNullOrWhiteSpace(sessionCode)) return Results.NotFound();

            // A device secret the caller generates and keeps. It is what lets the same phone come
            // back as the same participant after a reconnect, so it must not be server-assigned.
            if (string.IsNullOrWhiteSpace(request.DeviceSecret) || request.DeviceSecret.Length < 16)
                return Results.BadRequest(new { error = "A deviceSecret of at least 16 characters is required." });

            // Resolve doubles as the existence check: a code is only bound once CreateSession has
            // been applied, so an unknown or not-yet-created session is indistinguishable from a
            // wrong code to the caller.
            if (sessions.Resolve(sessionCode) is null) return Results.NotFound();

            try
            {
                var ticket = await joins.JoinAsync(sessionCode, request.DeviceSecret, request.DisplayName, ct);
                return Results.Ok(new JoinResponse(ticket.ParticipantId, ticket.SessionCode, ticket.Token, ticket.ExpiresAt));
            }
            catch (ArgumentException ex)
            {
                // Display-name rules live in ParticipantNameRules and are enforced at join now,
                // rather than one hub message later when the phone is already on the wall.
                return Results.BadRequest(new { error = ex.Message });
            }
        })
        .RequireRateLimiting("audience-join")
        .RequireCors("NuottiCors");
    }
}
