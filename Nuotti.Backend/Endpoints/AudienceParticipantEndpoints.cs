using Nuotti.Backend.Catalog;
using Nuotti.Backend.Participants;
using Nuotti.Backend.Sessions;
using Nuotti.Contracts.V1.Model;

namespace Nuotti.Backend.Endpoints;

public static class AudienceCatalogEndpoints
{
    public static void MapAudienceCatalogEndpoints(this WebApplication app)
    {
        app.MapGet("/api/sessions/{session}/catalog/search", async (
            string session,
            string? q,
            int? limit,
            IAudienceCatalogSearch search,
            CancellationToken ct) =>
        {
            if (string.IsNullOrWhiteSpace(session))
                return Results.BadRequest();
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
        app.MapPost("/api/sessions/{session}/participants/{participantId}/moderate-name", (
            string session,
            string participantId,
            ModerateParticipantNameRequest request,
            IParticipantIdentityStore participants) =>
        {
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
        app.MapGet("/status/{session}/answer", (
            IGameStateStore store,
            IParticipantIdentityStore participants,
            string session,
            string participantId) =>
        {
            if (string.IsNullOrWhiteSpace(participantId)
                || !participants.TryGet(session, participantId, out _))
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
