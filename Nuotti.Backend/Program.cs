using Microsoft.AspNetCore.SignalR;
using Nuotti.Backend;
using Nuotti.Contracts.V1.Enum;
using Nuotti.Contracts.V1.Message;
var builder = WebApplication.CreateBuilder(args);

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

var app = builder.Build();

// Enable CORS before mapping endpoints
app.UseCors("AllowAll");

// Map exceptions to NuottiProblem consistently
app.UseMiddleware<ProblemHandlingMiddleware>();

var sessions = new Dictionary<string, HashSet<string>>(); // sessionCode -> connIds

app.MapHub<QuizHub>("/hub").RequireCors("AllowAll");
app.MapPost("/api/sessions/{name}", (string name) => Results.Ok(new SessionCreated(name, Guid.NewGuid().ToString()))).RequireCors("AllowAll");
app.MapPost("/api/pushQuestion/{session}", async (IHubContext<QuizHub> hub, string session, QuestionPushed q) =>
{
    await hub.Clients.Group(session).SendAsync("QuestionPushed", q);
    return Results.Accepted();
}).RequireCors("AllowAll");
app.MapPost("/api/play/{session}", async (IHubContext<QuizHub> hub, string session, PlayTrack cmd) =>
{
    await hub.Clients.Group(session).SendAsync("PlayTrack", cmd);
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