using Nuotti.Backend.Commands;
using Nuotti.Backend.Exception;
using Nuotti.Backend.Middleware;
using Nuotti.Backend.Sessions;
using Nuotti.Contracts.V1.Enum;
using Nuotti.Contracts.V1.Event;
using Nuotti.Contracts.V1.Message;
using Nuotti.Contracts.V1.Message.Phase;
using Nuotti.Contracts.V1.Model;
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

        // Relay commands. The processor authorizes and publishes; LogStreamSubscriber writes the
        // dev log line and HubBroadcastSubscriber puts it on the wire.
        app.MapRelay<QuestionPushed>("/api/pushQuestion/{session}");
        app.MapRelay<PlayTrack>("/api/play/{session}");
        app.MapRelay<StopTrack>("/api/stop/{session}");

        // Upload a setlist manifest and replace the session's song catalog. Manifest validation and
        // catalog construction live behind the processor with every other command effect.
        app.MapPost("/api/manifest/{session}",
                async (HttpContext http, ISessionCommandProcessor processor, string session, SetlistManifest manifest) =>
                {
                    var cmd = new UpdateCatalog(manifest)
                    {
                        SessionCode = session,
                        IssuedByRole = Role.Performer,
                        IssuedById = "manifest-upload"
                    };

                    var result = await processor.ApplyAsync(
                        session,
                        Actor.Claimed(cmd),
                        cmd,
                        CorrelationIdMiddleware.GetCorrelationId(http),
                        http.RequestAborted);

                    if (result.Outcome == Outcome.Rejected) return ProblemResults.From(result.Problem!);

                    IReadOnlyList<SongRef> catalog = result.State?.Catalog ?? [];
                    return Results.Accepted($"/status/{session}", new { catalog });
                })
            .RequireCors("NuottiCors");

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

    static void MapRelay<T>(this WebApplication app, string route) where T : CommandBase
    {
        app.MapPost(route, async (HttpContext http, ISessionCommandProcessor processor, string session, T cmd) =>
            {
                var result = await processor.ApplyAsync(
                    session,
                    Actor.Claimed(cmd),
                    cmd,
                    CorrelationIdMiddleware.GetCorrelationId(http),
                    http.RequestAborted);

                return result.ToHttpResult();
            })
            .RequireCors("NuottiCors");
    }
}
