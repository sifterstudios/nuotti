using Microsoft.AspNetCore.SignalR;
using Nuotti.Backend.Exception;
using Nuotti.Backend.Sessions;
using Nuotti.Contracts.V1.Enum;
using Nuotti.Contracts.V1.Event;
using Nuotti.Contracts.V1.Message;
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
        }).RequireCors("AllowAll");

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
        }).RequireCors("AllowAll");
        
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
        }).RequireCors("AllowAll");

        app.MapGet("/api/demo/problem/{kind}", (string kind) =>
        {
            return kind.ToLowerInvariant() switch
            {
                "400" or "badrequest" => ProblemResults.BadRequest("Invalid input", "Name must not be empty", ReasonCode.InvalidStateTransition, "name"),
                "409" or "conflict" => ProblemResults.Conflict("Duplicate command", "Operation already performed", ReasonCode.DuplicateCommand),
                "422" or "unprocessable" => ProblemResults.UnprocessableEntity("Business rule violated", "Performer cannot submit an answer", ReasonCode.UnauthorizedRole, "issuedByRole"),
                _ => Results.NotFound()
            };
        }).RequireCors("AllowAll");
    }
}