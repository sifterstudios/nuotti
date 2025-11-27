using Nuotti.Backend;
using Nuotti.Backend.Endpoints;
using Nuotti.Backend.Eventing;
using Nuotti.Backend.Eventing.Subscribers;
using Nuotti.Backend.Exception;
using Nuotti.Backend.Idempotency;
using Nuotti.Backend.Models;
using Nuotti.Backend.Sessions;
using Nuotti.Contracts.V1.Eventing;
using Serilog;
var builder = WebApplication.CreateBuilder(args);
builder.Services.AddEndpointsApiExplorer();
builder.AddServiceDefaults();
builder.ConfigureStructuredLogging();

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
            var opts = builder.Configuration.Get<NuottiOptions>();
            var origins = (opts?.AllowedOrigins ?? string.Empty)
                .Split([',', ';'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

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
builder.Services.AddSingleton<ISessionStore, InMemorySessionStore>();
builder.Services.AddSingleton<IGameStateStore, InMemoryGameStateStore>();
builder.Services.AddSingleton<IIdempotencyStore, InMemoryIdempotencyStore>();

// Event bus and subscribers
builder.Services.AddSingleton<IEventBus, InMemoryEventBus>();
builder.Services.AddSingleton<StateApplySubscriber>();
builder.Services.AddSingleton<HubBroadcastSubscriber>();
builder.Services.AddSingleton<MetricsSubscriber>();

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
app.MapDevEndpoints();
app.MapDefaultEndpoints();

// Force creation of subscribers so they can attach to the bus
_ = app.Services.GetRequiredService<StateApplySubscriber>();
_ = app.Services.GetRequiredService<HubBroadcastSubscriber>();
_ = app.Services.GetRequiredService<MetricsSubscriber>();

// Log startup with common fields
var logger = app.Services.GetRequiredService<ILogger<Program>>();
logger.LogInformation("Backend started. Service={Service}, Version={Version}", "Nuotti.Backend", "1.0.0");

try
{
    app.Run();
}
finally
{
    Log.CloseAndFlush();
}

// Expose Program for WebApplicationFactory in tests
public partial class Program { }