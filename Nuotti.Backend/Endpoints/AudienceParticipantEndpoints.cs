using Nuotti.Backend.Catalog;
using Nuotti.Backend.Participants;
using Nuotti.Backend.Realtime;
using Nuotti.Backend.Sessions;
using Nuotti.Backend.Workspaces;
using Nuotti.Contracts.V1.Model;

namespace Nuotti.Backend.Endpoints;

public static class AudienceCatalogEndpoints
{
    public static void MapAudienceCatalogEndpoints(this WebApplication app)
    {
        app.MapGet("/api/sessions/{session}/catalog/search", async (
            HttpContext http,
            string session,
            string? q,
            int? limit,
            IAudienceCatalogSearch search,
            IAudienceJoinStore joins,
            CancellationToken ct) =>
        {
            if (string.IsNullOrWhiteSpace(session))
                return Results.BadRequest();
            // A band's private catalog is a real asset. Searching it requires having joined the
            // session it belongs to, not merely knowing its code.
            if (await AudienceHttpAccess.AuthenticateAsync(http, joins, session, ct) is null)
                return Results.Unauthorized();
            var results = await search.SearchAsync(session, q ?? string.Empty, limit ?? 25, ct);
            return Results.Ok(results);
        }).RequireCors("NuottiCors");
    }
}

public static class ParticipantEndpoints
{
    public sealed record ModerateParticipantNameRequest(string DisplayName);

    public static void MapParticipantEndpoints(this WebApplication app)
    {
        app.MapPost("/api/sessions/{session}/participants/{participantId}/moderate-name", async (
            HttpContext http,
            string session,
            string participantId,
            ModerateParticipantNameRequest request,
            IParticipantIdentityStore participants,
            IWorkspaceAccessStore workspaces,
            ISessionWorkspaceBinder sessions,
            CancellationToken ct) =>
        {
            // Moderation is the band's job, not the crowd's: renaming somebody else's phone is a
            // Performer action, so it needs a member of the workspace that owns the session.
            var workspaceId = sessions.Resolve(session);
            if (workspaceId is null) return Results.NotFound();
            var selected = await WorkspaceHttpAccess.RequireSelectedAsync(http, workspaces, workspaceId, ct);
            if (selected.Principal is null) return Results.Unauthorized();
            if (selected.Access is null) return Results.NotFound();

            try
            {
                if (!participants.TryModerateName(session, participantId, request.DisplayName, out var moderated)
                    || moderated is null)
                {
                    return Results.NotFound();
                }

                return Results.Ok(moderated);
            }
            catch (ArgumentException ex)
            {
                return Results.BadRequest(new { error = ex.Message });
            }
        }).RequireCors("NuottiCors");
    }
}

public static class AudienceAnswerStatusEndpoints
{
    public sealed record MyAnswerResponse(int? ChoiceIndex);

    public static void MapAudienceAnswerStatusEndpoints(this WebApplication app)
    {
        app.MapGet("/status/{session}/answer", async (
            HttpContext http,
            IGameStateStore store,
            IParticipantIdentityStore participants,
            IAudienceJoinStore joins,
            string session,
            CancellationToken ct) =>
        {
            // The participant is the token's, never the query string's. Taking it from the URL
            // meant one phone could read what another had answered while the round was still open.
            var identity = await AudienceHttpAccess.AuthenticateAsync(http, joins, session, ct);
            if (identity is null) return Results.Unauthorized();

            var participantId = identity.ParticipantId;
            if (!participants.TryGet(session, participantId, out _))
            {
                return Results.NotFound();
            }

            if (!store.TryGet(session, out GameStateSnapshot snapshot))
            {
                return Results.NotFound();
            }

            snapshot.Answers.TryGetValue(participantId, out var choice);
            return Results.Ok(new MyAnswerResponse(snapshot.Answers.ContainsKey(participantId) ? choice : null));
        }).RequireCors("NuottiCors");
    }
}
