using Microsoft.AspNetCore.SignalR;
using Nuotti.Backend.Exception;
using Nuotti.Backend.Sessions;
using Nuotti.Contracts.V1.Enum;
using Nuotti.Contracts.V1.Event;
using Nuotti.Contracts.V1.Message;
using Nuotti.Contracts.V1.Model;
using System.ComponentModel.DataAnnotations;
namespace Nuotti.Backend.Endpoints;

internal static class ApiEndpoints
{
    public static void MapApiEndpoints(this WebApplication app)
    {
        app.MapPost("/api/sessions/{name}", async (string name, ILogStreamer log) =>
        {
            var session = new SessionCreated(name, Guid.NewGuid().ToString());
            await log.BroadcastAsync(new LogEvent(
                Timestamp: DateTimeOffset.UtcNow,
                Level: "Info",
                Source: "Program",
                Message: $"Session created: code={session.SessionCode} hostId={session.HostId}",
                Session: session.SessionCode
            ));
            return Results.Ok(session);
        }).RequireCors("NuottiCors");

        app.MapGet("/api/sessions/{session}/counts", (ISessionStore store, string session) =>
        {
            var counts = store.GetCounts(session);
            return Results.Ok(new
            {
                performer = counts.Performer,
                projector = counts.Projector,
                engine = counts.Engine,
                audiences = counts.Audiences
            });
        }).RequireCors("NuottiCors");

        app.MapPost("/api/pushQuestion/{session}", async (IHubContext<QuizHub> hub, ILogStreamer log, string session, QuestionPushed q) =>
        {
            if (q.IssuedByRole != Role.Performer) { return ProblemResults.WrongRoleTriedExecutingResult(Role.Performer); }
            
            await hub.Clients.Group(session).SendAsync("QuestionPushed", q);
            await log.BroadcastAsync(new LogEvent(
                Timestamp: DateTimeOffset.UtcNow,
                Level: "Info",
                Source: "Program",
                Message: $"QuestionPushed to session={session}: {q.Text}",
                Session: session
            ));
            return Results.Accepted();
        }).RequireCors("NuottiCors");

        app.MapPost("/api/play/{session}", async (IHubContext<QuizHub> hub, ILogStreamer log, string session, PlayTrack cmd) =>
        {
            if (cmd.IssuedByRole != Role.Performer) { return ProblemResults.WrongRoleTriedExecutingResult(Role.Performer); }
            
            await hub.Clients.Group(session).SendAsync("PlayTrack", cmd);
            await log.BroadcastAsync(new LogEvent(
                Timestamp: DateTimeOffset.UtcNow,
                Level: "Info",
                Source: "Program",
                Message: $"Play requested for session={session}: url={cmd.FileUrl}",
                Session: session
            ));
            return Results.Accepted();
        }).RequireCors("NuottiCors");
        
        app.MapPost("/api/stop/{session}", async (IHubContext<QuizHub> hub, ILogStreamer log, string session, StopTrack cmd) =>
        {
            if (cmd.IssuedByRole != Role.Performer) { return ProblemResults.WrongRoleTriedExecutingResult(Role.Performer); }
            
            await hub.Clients.Group(session).SendAsync("Stop", cmd);
            await log.BroadcastAsync(new LogEvent(
                Timestamp: DateTimeOffset.UtcNow,
                Level: "Info",
                Source: "Program",
                Message: $"Stop requested for session={session}",
                Session: session
            ));
            return Results.Accepted();
        }).RequireCors("NuottiCors");

        // Upload setlist manifest and update server-side state with the song catalog
        app.MapPost("/api/manifest/{session}", (IGameStateStore game, string session, SetlistManifest manifest) =>
        {
            // Basic validation
            if (manifest?.Songs == null || manifest.Songs.Count == 0)
            {
                return ProblemResults.UnprocessableEntity("Invalid manifest", "At least one song is required.");
            }

            // Validate each song using DataAnnotations
            for (var i = 0; i < manifest.Songs.Count; i++)
            {
                var song = manifest.Songs[i];
                var ctx = new ValidationContext(song);
                var results = new List<ValidationResult>();
                if (!Validator.TryValidateObject(song, ctx, results, validateAllProperties: true))
                {
                    var first = results.First();
                    var field = first.MemberNames.FirstOrDefault();
                    return ProblemResults.UnprocessableEntity("Invalid manifest", first.ErrorMessage ?? "Validation failed", ReasonCode.None, field);
                }
            }

            // Build catalog from manifest
            static string Slug(string s)
                => new string((s ?? string.Empty).ToLowerInvariant().Where(ch => char.IsLetterOrDigit(ch) || ch == '-').ToArray());

            var catalog = manifest.Songs
                .Select((s, i) => new SongRef(
                    new SongId($"song-{i + 1}-{Slug(s.Title)}"),
                    s.Title,
                    s.Artist ?? string.Empty))
                .ToArray();

            // Update or create snapshot with catalog
            var snapshot = game.GetOrCreate(session, sess => new GameStateSnapshot(
                sessionCode: sess,
                phase: Phase.Idle,
                songIndex: 0,
                currentSong: null,
                catalog: catalog,
                choices: Array.Empty<string>(),
                hintIndex: 0,
                tallies: Array.Empty<int>(),
                scores: null,
                songStartedAtUtc: null));

            // If snapshot exists, replace with same values but updated catalog
            snapshot = snapshot with { Catalog = catalog };
            game.Set(session, snapshot);

            return Results.Accepted($"/status/{session}", new { catalog });
        }).RequireCors("NuottiCors");

        app.MapGet("/api/demo/problem/{kind}", (HttpContext ctx, string kind) =>
        {
            Guid? correlationId = null;
            if (ctx.Request.Headers.TryGetValue("X-Correlation-Id", out var values) && Guid.TryParse(values.ToString(), out var parsed))
            {
                correlationId = parsed;
            }

            return kind.ToLowerInvariant() switch
            {
                "400" or "badrequest" => ProblemResults.BadRequest("Invalid input", "Name must not be empty", ReasonCode.InvalidStateTransition, "name", correlationId),
                "409" or "conflict" => ProblemResults.Conflict("Duplicate command", "Operation already performed", ReasonCode.DuplicateCommand, null, correlationId),
                "422" or "unprocessable" => ProblemResults.UnprocessableEntity("Business rule violated", "Performer cannot submit an answer", ReasonCode.UnauthorizedRole, "issuedByRole", correlationId),
                _ => Results.NotFound()
            };
        }).RequireCors("NuottiCors");
    }
}