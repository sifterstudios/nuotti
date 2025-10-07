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

// CORS: environment-based policy
builder.Services.AddCors(options =>
{
    options.AddPolicy("NuottiCors", policy =>
    {
        if (builder.Environment.IsDevelopment())
        {
            // Allow http(s)://localhost:* with credentials for dev
            policy
                .SetIsOriginAllowed(origin =>
                {
                    if (string.IsNullOrWhiteSpace(origin)) return false;
                    if (!Uri.TryCreate(origin, UriKind.Absolute, out var uri)) return false;
                    var isLocalhost = string.Equals(uri.Host, "localhost", StringComparison.OrdinalIgnoreCase);
                    var isHttp = uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps;
                    return isLocalhost && isHttp; // any port
                })
                .AllowAnyHeader()
                .AllowAnyMethod()
                .AllowCredentials();
        }
        else
        {
            // Production: allowlist via config
            var opts = builder.Configuration.Get<Nuotti.Backend.Models.NuottiOptions>();
            var origins = (opts?.AllowedOrigins ?? string.Empty)
                .Split(new[] { ',', ';' }, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

            if (origins.Length > 0)
            {
                policy
                    .WithOrigins(origins)
                    .AllowAnyHeader()
                    .AllowAnyMethod()
                    .AllowCredentials();
            }
            else
            {
                // No origins configured -> deny cross-origin by default
                policy.DisallowCredentials();
            }
        }
    });
});

builder.Services.AddSingleton<ILogStreamer, LogStreamer>();
builder.Services.AddSingleton<Nuotti.Backend.Sessions.ISessionStore, Nuotti.Backend.Sessions.InMemorySessionStore>();
builder.Services.AddSingleton<Nuotti.Backend.Sessions.IGameStateStore, Nuotti.Backend.Sessions.InMemoryGameStateStore>();
builder.Services.AddSingleton<Nuotti.Backend.Idempotency.IIdempotencyStore, Nuotti.Backend.Idempotency.InMemoryIdempotencyStore>();

var app = builder.Build();

app.UseCors("NuottiCors");
app.UseMiddleware<ProblemHandlingMiddleware>();
app.MapPhaseEndpoints();

app.MapHub<QuizHub>("/hub").RequireCors("NuottiCors");
if (app.Environment.IsDevelopment())
{
    app.MapHub<LogHub>("/log").RequireCors("NuottiCors");
}
app.MapApiEndpoints();
app.MapHealthEndpoints();
app.MapStatusEndpoints();

app.Run();

// Expose Program for WebApplicationFactory in tests
public partial class Program { }