using Microsoft.AspNetCore.SignalR;
using Nuotti.Backend;
using Nuotti.Backend.Endpoints;
using Nuotti.Backend.Exception;
using Nuotti.Backend.Models;
using Nuotti.Contracts.V1.Enum;
using Nuotti.Contracts.V1.Message;
using Nuotti.Contracts.V1.Event;
using Nuotti.Contracts.V1.Reducer;
using Nuotti.Contracts.V1.Model;
using Nuotti.Contracts.V1.Message.Phase;
using System.Collections.Concurrent;
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
builder.Services.AddSingleton<Nuotti.Backend.Idempotency.IIdempotencyStore, Nuotti.Backend.Idempotency.InMemoryIdempotencyStore>();

var app = builder.Build();

// Enable CORS before mapping endpoints
app.UseCors("AllowAll");

// Map exceptions to NuottiProblem consistently
app.UseMiddleware<ProblemHandlingMiddleware>();

// Map Phase endpoints (extracted)
app.MapPhaseEndpoints();

app.MapHub<QuizHub>("/hub").RequireCors("AllowAll");
if (app.Environment.IsDevelopment())
{
    app.MapHub<LogHub>("/log").RequireCors("AllowAll");
}
// Map other API endpoints (extracted)
app.MapApiEndpoints();

app.Run();

// Expose Program for WebApplicationFactory in tests
public partial class Program { }