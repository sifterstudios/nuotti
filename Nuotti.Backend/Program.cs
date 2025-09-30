using Nuotti.Backend;
using Microsoft.AspNetCore.SignalR;
using Nuotti.Contracts.V1;
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

app.Run();