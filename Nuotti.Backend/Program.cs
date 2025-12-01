using Nuotti.Backend;
using Nuotti.Backend.Endpoints;
using Nuotti.Backend.Eventing;
using Nuotti.Backend.Eventing.Subscribers;
using Nuotti.Backend.Exception;
using Nuotti.Backend.Idempotency;
using Nuotti.Backend.Metrics;
using Nuotti.Backend.Models;
using Nuotti.Backend.Sessions;
using Nuotti.Contracts.V1.Eventing;
using Microsoft.Extensions.Options;
using Serilog;
var builder = WebApplication.CreateBuilder(args);
builder.Services.AddEndpointsApiExplorer();
builder.AddServiceDefaults();
builder.ConfigureStructuredLogging();

// Add service-specific health checks
builder.Services.AddHealthChecks()
    .AddCheck<Nuotti.Backend.HealthChecks.SessionStoreHealthCheck>("sessionstore", tags: ["ready"]);

// Configuration: JSON + env vars (NUOTTI_ prefix). Bind strongly-typed options from "Nuotti" section.
builder.Configuration
    .AddJsonFile("appsettings.json", optional: true, reloadOnChange: true)
    .AddJsonFile($"appsettings.{builder.Environment.EnvironmentName}.json", optional: true, reloadOnChange: true)
    .AddEnvironmentVariables(prefix: "NUOTTI_");

builder.Services
    .AddOptions<NuottiOptions>()
    .Bind(builder.Configuration)
    .ValidateOnStart();
builder.Services.AddSingleton<IValidateOptions<NuottiOptions>, NuottiOptionsValidator>();

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
            var origins = builder.Configuration.GetValue<string>("Nuotti:AllowedOrigins", string.Empty)
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

// Metrics
builder.Services.AddSingleton<BackendMetrics>();

// Diagnostics
builder.Services.AddSingleton<Nuotti.Backend.Diagnostics.DiagnosticsBundleService>();

// Alerting
builder.Services.AddHttpClient("Alerting");
builder.Services.AddSingleton<Nuotti.Backend.Alerting.CriticalRoleAlertingService>();

// Time drift checking
builder.Services.AddSingleton<Nuotti.Backend.TimeDrift.TimeDriftService>();

// Audit logging - create separate audit logger with file sink
var auditLogDir = ServiceDefaults.LogFileHelper.GetLogDirectory("Nuotti.Backend");
var auditLogPath = Path.Combine(auditLogDir, "audit-.log");
var auditLogger = new Serilog.LoggerConfiguration()
    .MinimumLevel.Information()
    .Enrich.WithProperty("service", "Nuotti.Backend")
    .Enrich.WithProperty("audit", true)
    .Enrich.FromLogContext()
    .WriteTo.File(
        new Serilog.Formatting.Json.JsonFormatter(renderMessage: true),
        auditLogPath,
        rollingInterval: RollingInterval.Day,
        retainedFileCountLimit: 30, // Keep 30 days of audit logs
        fileSizeLimitBytes: 100_000_000, // 100MB per file
        rollOnFileSizeLimit: true)
    .CreateLogger();
builder.Services.AddSingleton<Serilog.ILogger>(provider => auditLogger);
builder.Services.AddSingleton<Nuotti.Backend.Audit.AuditLogService>();

// Event bus and subscribers
builder.Services.AddSingleton<IEventBus, InMemoryEventBus>();
builder.Services.AddSingleton<StateApplySubscriber>();
builder.Services.AddSingleton<HubBroadcastSubscriber>();
builder.Services.AddSingleton<MetricsSubscriber>();

var app = builder.Build();

app.UseCors("NuottiCors");
app.UseMiddleware<Nuotti.Backend.Middleware.CorrelationIdMiddleware>();
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
app.MapMetricsEndpoints();
app.MapAboutEndpoints();
app.MapTimeEndpoints();
app.MapDiagnosticsEndpoints();
app.MapDevEndpoints();
app.MapDefaultEndpoints();

// Force creation of subscribers so they can attach to the bus
_ = app.Services.GetRequiredService<StateApplySubscriber>();
_ = app.Services.GetRequiredService<HubBroadcastSubscriber>();
_ = app.Services.GetRequiredService<MetricsSubscriber>();

// Log startup with version info
var logger = app.Services.GetRequiredService<ILogger<Program>>();
var versionInfo = ServiceDefaults.VersionInfo.GetVersionInfo("Nuotti.Backend");
var features = ServiceDefaults.FeatureFlags.GetAll(app.Configuration);
var enabledFeatures = features.Where(f => f.Value).Select(f => f.Key).ToList();
logger.LogInformation("Backend started. Service={Service}, Version={Version}, GitCommit={GitCommit}, BuildTime={BuildTime}, Runtime={Runtime}, EnabledFeatures=[{EnabledFeatures}]",
    versionInfo.Service, versionInfo.Version, versionInfo.GitCommit, versionInfo.BuildTime, versionInfo.Runtime, string.Join(", ", enabledFeatures));

// Check time drift at startup
try
{
    var timeDriftService = app.Services.GetRequiredService<Nuotti.Backend.TimeDrift.TimeDriftService>();
    var driftResult = timeDriftService.CheckTimeDrift();
    if (driftResult.Success)
    {
        var driftClassification = Nuotti.Backend.TimeDrift.TimeDriftService.ClassifyDrift(driftResult.DriftMs);
        logger.LogInformation("Time drift check. DriftMs={DriftMs:F2}, Classification={Classification}, NtpServer={NtpServer}, LocalTime={LocalTime:O}, NtpTime={NtpTime:O}",
            driftResult.DriftMs, driftClassification, driftResult.NtpServer, driftResult.LocalTime, driftResult.NtpTime);

        if (Math.Abs(driftResult.DriftMs) > 250)
        {
            logger.LogWarning("Significant time drift detected. DriftMs={DriftMs:F2}, Classification={Classification}",
                driftResult.DriftMs, driftClassification);
        }
    }
    else
    {
        logger.LogWarning("Time drift check failed. Error={Error}", driftResult.Error ?? "Unknown error");
    }
}
catch (Exception ex)
{
    logger.LogWarning(ex, "Failed to check time drift at startup: {Message}", ex.Message);
}

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
