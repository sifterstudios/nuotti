using Microsoft.Extensions.Options;
using Nuotti.Backend.Realtime;
using Nuotti.Backend.Sessions;
using Nuotti.Contracts.V1.Model;

namespace Nuotti.Backend.Endpoints;

/// <summary>
/// The catch-up read: what is happening in this session right now.
/// </summary>
/// <remarks>
/// This was Development-only, and three shipped clients call it. Deployed, that meant a phone
/// joining mid-round saw a blank screen until the next broadcast happened to fire, and the
/// Projector's reconnect resync failed silently every time - it reconnected and then waited for an
/// event instead of catching up. It is not simply un-gated: a snapshot carries the current question
/// and the running tallies, so it needs the same credential the hub does.
/// </remarks>
internal static class StatusEndpoints
{
    public static void MapStatusEndpoints(this WebApplication app)
    {
        app.MapGet("/status/{session}", async (
            HttpContext http, IGameStateStore store, IConnectionPrincipalResolver principals,
            IOptions<RealtimeOptions> realtime, string session, CancellationToken ct) =>
        {
            // Honours the same escape hatch as the hub rather than inventing a second rule: a
            // deployment either requires credentials of its realtime callers or it does not, and
            // the local loop still has clients that predate them.
            var principal = await RealtimeHttpAccess.ResolveAsync(http, principals, session, ct);
            if (principal is null && !realtime.Value.AllowUnauthenticatedConnections)
                return Results.Unauthorized();
            if (principal is not null && !principal.Can(Capability.Subscribe)) return Results.Unauthorized();

            return store.TryGet(session, out GameStateSnapshot snapshot)
                ? Results.Ok(snapshot)
                : Results.NotFound();
        }).RequireCors("NuottiCors");
    }
}
