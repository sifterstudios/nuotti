using Microsoft.AspNetCore.SignalR;
using Nuotti.Backend;
using Nuotti.Backend.Sessions;
using Nuotti.Contracts.V1.Message;
using Nuotti.Contracts.V1.Model;
using Nuotti.Contracts.V1.Event;

namespace Microsoft.AspNetCore.Builder;

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
        }).RequireCors("AllowAll");

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
        }).RequireCors("AllowAll");

        app.MapPost("/api/pushQuestion/{session}", async (IHubContext<QuizHub> hub, ILogStreamer log, string session, QuestionPushed q) =>
        {
            await hub.Clients.Group(session).SendAsync("QuestionPushed", q);
            await log.BroadcastAsync(new LogEvent(
                Timestamp: DateTimeOffset.UtcNow,
                Level: "Info",
                Source: "Program",
                Message: $"QuestionPushed to session={session}: {q.Text}",
                Session: session
            ));
            return Results.Accepted();
        }).RequireCors("AllowAll");

        app.MapPost("/api/play/{session}", async (IHubContext<QuizHub> hub, ILogStreamer log, string session, PlayTrack cmd) =>
        {
            await hub.Clients.Group(session).SendAsync("PlayTrack", cmd);
            await log.BroadcastAsync(new LogEvent(
                Timestamp: DateTimeOffset.UtcNow,
                Level: "Info",
                Source: "Program",
                Message: $"Play requested for session={session}: url={cmd.FileUrl}",
                Session: session
            ));
            return Results.Accepted();
        }).RequireCors("AllowAll");

        // Demo endpoints returning NuottiProblem directly
        app.MapGet("/api/demo/problem/{kind}", (string kind) =>
        {
            return kind.ToLowerInvariant() switch
            {
                "400" or "badrequest" => Nuotti.Backend.ProblemResults.BadRequest("Invalid input", "Name must not be empty", Nuotti.Contracts.V1.Enum.ReasonCode.InvalidStateTransition, "name"),
                "409" or "conflict" => Nuotti.Backend.ProblemResults.Conflict("Duplicate command", "Operation already performed", Nuotti.Contracts.V1.Enum.ReasonCode.DuplicateCommand),
                "422" or "unprocessable" => Nuotti.Backend.ProblemResults.UnprocessableEntity("Business rule violated", "Performer cannot submit an answer", Nuotti.Contracts.V1.Enum.ReasonCode.UnauthorizedRole, "issuedByRole"),
                _ => Results.NotFound()
            };
        }).RequireCors("AllowAll");
    }
}
