using Microsoft.AspNetCore.SignalR;
using Nuotti.Backend;
using Nuotti.Backend.Models;
using Nuotti.Contracts.V1.Enum;
using Nuotti.Contracts.V1.Message;
using Nuotti.Contracts.V1.Event;
var builder = WebApplication.CreateBuilder(args);

// Configuration: JSON + env vars (NUOTTI_ prefix). Bind strongly-typed options from "Nuotti" section.
builder.Configuration
    .AddJsonFile("appsettings.json", optional: true, reloadOnChange: true)
    .AddJsonFile($"appsettings.{builder.Environment.EnvironmentName}.json", optional: true, reloadOnChange: true)
    .AddEnvironmentVariables(prefix: "NUOTTI_");

builder.Services
    .AddOptions<NuottiOptions>()
    .Bind(builder.Configuration)
    .ValidateOnStart();

builder.Services
    .AddSignalR(o =>
    {
        o.EnableDetailedErrors = true;
    })
    .AddJsonProtocol(o => o.PayloadSerializerOptions.PropertyNamingPolicy = null);

// CORS: allow cross-origin dev clients (Audience/Projector) to call the Backend
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowAll", policy =>
    {
        policy
            .AllowAnyOrigin()
            .AllowAnyHeader()
            .AllowAnyMethod();
    });
});

builder.Services.AddSingleton<ILogStreamer, LogStreamer>();
builder.Services.AddSingleton<Nuotti.Backend.Sessions.ISessionStore, Nuotti.Backend.Sessions.InMemorySessionStore>();

var app = builder.Build();

// Enable CORS before mapping endpoints
app.UseCors("AllowAll");

// Map exceptions to NuottiProblem consistently
app.UseMiddleware<ProblemHandlingMiddleware>();

var sessions = new Dictionary<string, HashSet<string>>(); // sessionCode -> connIds

app.MapHub<QuizHub>("/hub").RequireCors("AllowAll");
if (app.Environment.IsDevelopment())
{
    app.MapHub<LogHub>("/log").RequireCors("AllowAll");
}
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

app.MapGet("/api/sessions/{session}/counts", (Nuotti.Backend.Sessions.ISessionStore store, string session) =>
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
        "400" or "badrequest" => ProblemResults.BadRequest("Invalid input", "Name must not be empty", ReasonCode.InvalidStateTransition, "name"),
        "409" or "conflict" => ProblemResults.Conflict("Duplicate command", "Operation already performed", ReasonCode.DuplicateCommand),
        "422" or "unprocessable" => ProblemResults.UnprocessableEntity("Business rule violated", "Performer cannot submit an answer", ReasonCode.UnauthorizedRole, "issuedByRole"),
        _ => Results.NotFound()
    };
}).RequireCors("AllowAll");

app.Run();