using Nuotti.Backend;
using Nuotti.Backend.Catalog;
using Nuotti.Backend.Commands;
using Nuotti.Backend.Endpoints;
using Nuotti.Backend.Eventing;
using Nuotti.Backend.Eventing.Subscribers;
using Nuotti.Backend.Exception;
using Nuotti.Backend.Idempotency;
using Nuotti.Backend.InfrastructureProof;
using Nuotti.Backend.Metrics;
using Nuotti.Backend.Models;
using Nuotti.Backend.Participants;
using Nuotti.Backend.Persistence;
using Nuotti.Backend.Sessions;
using Nuotti.Backend.Workspaces;
using Nuotti.Backend.ShowAgents;
using Nuotti.Backend.Assets;
using Nuotti.Backend.SongPackages;
using Nuotti.Backend.SessionSnapshots;
using Nuotti.Backend.Setlists;
using Nuotti.Contracts.V1.Eventing;
using Microsoft.Extensions.Options;
using System.Threading.RateLimiting;
using Serilog;
using ServiceDefaults;
var builder = WebApplication.CreateBuilder(args);
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    options.AddPolicy("show-agent-pairing", http => RateLimitPartition.GetFixedWindowLimiter(
        http.Connection.RemoteIpAddress?.ToString() ?? "unknown",
        _ => new FixedWindowRateLimiterOptions
        {
            PermitLimit = 10,
            Window = TimeSpan.FromMinutes(1),
            QueueLimit = 0,
            AutoReplenishment = true
        }));
});
builder.AddNuottiWebHost(enableFileSink: false);

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

var signalR = builder.Services
    .AddSignalR(o =>
    {
        o.EnableDetailedErrors = true;
    })
    .AddJsonProtocol(o => o.PayloadSerializerOptions.PropertyNamingPolicy = null);

var databaseConnection = builder.Configuration.GetConnectionString("nuotti");
var realtimeConnection = builder.Configuration.GetConnectionString("realtime");
var assetsConnection = builder.Configuration.GetConnectionString("assets");
if (!string.IsNullOrWhiteSpace(databaseConnection)) builder.AddNpgsqlDataSource("nuotti");
if (!string.IsNullOrWhiteSpace(assetsConnection)) builder.AddAzureBlobServiceClient("assets");
if (!string.IsNullOrWhiteSpace(realtimeConnection))
{
    builder.AddRedisClient("realtime");
    signalR.AddStackExchangeRedis(realtimeConnection);
}

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
// Magic-link delivery. Mailgun wins when its section is configured; otherwise the internal
// webhook adapter stays in place. Neither is a no-op: with nothing configured, sign-in fails
// loudly rather than pretending a mail was sent.
builder.Services.AddHttpClient(nameof(HttpMagicLinkDelivery));
builder.Services.AddHttpClient(MailgunMagicLinkDelivery.HttpClientName);
builder.Services
    .AddOptions<MailgunOptions>()
    .Bind(builder.Configuration.GetSection(MailgunOptions.SectionName))
    .ValidateOnStart();
builder.Services.AddSingleton<IValidateOptions<MailgunOptions>, MailgunOptionsValidator>();
if (builder.Configuration.GetSection(MailgunOptions.SectionName).Get<MailgunOptions>()?.IsConfigured == true)
    builder.Services.AddSingleton<IMagicLinkDelivery, MailgunMagicLinkDelivery>();
else
    builder.Services.AddSingleton<IMagicLinkDelivery, HttpMagicLinkDelivery>();
builder.Services.AddSingleton<ISessionStore>(sp => new InMemorySessionStore(
    sp.GetRequiredService<IOptions<NuottiOptions>>(),
    sp.GetRequiredService<IGameStateStore>()));
builder.Services.AddSingleton<IGameStateStore, InMemoryGameStateStore>();
builder.Services.AddSingleton<IIdempotencyStore, InMemoryIdempotencyStore>();
builder.Services.AddSingleton<ISessionWorkspaceBinder, InMemorySessionWorkspaceBinder>();
builder.Services.AddSingleton<IParticipantIdentityStore, InMemoryParticipantIdentityStore>();
builder.Services.AddSingleton<ISharedSongCatalog, InMemorySharedSongCatalog>();
builder.Services.AddSingleton<IAudienceCatalogSearch, AudienceCatalogSearch>();
if (!string.IsNullOrWhiteSpace(databaseConnection))
{
    builder.Services.AddSingleton<IWorkspaceAccessStore, PostgresWorkspaceAccessStore>();
    builder.Services.AddSingleton<IShowAgentAccessStore, PostgresShowAgentAccessStore>();
    builder.Services.AddSingleton<IPrivateAssetMetadataStore, PostgresPrivateAssetMetadataStore>();
    builder.Services.AddSingleton<ISongPackageStore, PostgresSongPackageStore>();
    builder.Services.AddSingleton<ISessionSetlistSnapshotStore, PostgresSessionSetlistSnapshotStore>();
    builder.Services.AddSingleton<IWorkspaceSetlistStore, PostgresWorkspaceSetlistStore>();
    builder.Services.AddSingleton<ILyricTrackRevisionStore, PostgresLyricTrackRevisionStore>();
    builder.Services.AddSingleton<IDurableSessionCommitStore, PostgresDurableSessionCommitStore>();
}
else
{
    // Keep the same durable command/recovery path available during local development and
    // isolated tests. Production replaces only the adapter, not the authorization boundary.
    builder.Services.AddSingleton<IWorkspaceAccessStore, InMemoryWorkspaceAccessStore>();
    builder.Services.AddSingleton<IShowAgentAccessStore, InMemoryShowAgentAccessStore>();
    builder.Services.AddSingleton<IPrivateAssetMetadataStore, InMemoryPrivateAssetMetadataStore>();
    builder.Services.AddSingleton<ISongPackageStore, InMemorySongPackageStore>();
    builder.Services.AddSingleton<ISessionSetlistSnapshotStore, InMemorySessionSetlistSnapshotStore>();
    builder.Services.AddSingleton<IWorkspaceSetlistStore, InMemoryWorkspaceSetlistStore>();
    builder.Services.AddSingleton<ILyricTrackRevisionStore, InMemoryLyricTrackRevisionStore>();
    builder.Services.AddSingleton<IDurableSessionCommitStore, InMemoryDurableSessionCommitStore>();
}
if (!string.IsNullOrWhiteSpace(assetsConnection))
    builder.Services.AddSingleton<IPrivateAssetObjectStore, AzurePrivateAssetObjectStore>();
else
    builder.Services.AddSingleton<IPrivateAssetObjectStore, InMemoryPrivateAssetObjectStore>();
builder.Services.AddSingleton<SongPackageReadinessEvaluator>();
builder.Services.AddSingleton<SessionSnapshotBuilder>();
builder.Services.AddSingleton<IScoringPolicyCatalog, MvpScoringPolicyCatalog>();
builder.Services.AddSingleton<DurableOutboxDispatcher>();
builder.Services.AddHostedService<DurableOutboxWorker>();

// Metrics
builder.Services.AddSingleton<BackendMetrics>();

// Diagnostics
builder.Services.AddSingleton<Nuotti.Backend.Diagnostics.DiagnosticsBundleService>();
builder.Services.AddSingleton<Nuotti.Backend.Governance.ProductionGovernance>();

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
builder.Services.AddSingleton<Nuotti.Backend.Retention.ISessionResultsStore, Nuotti.Backend.Retention.InMemorySessionResultsStore>();

// Command processing: the only path by which a session's state changes.
builder.Services.AddSingleton<ISessionCommandProcessor, SessionCommandProcessor>();

// Event bus and subscribers. The bus is the only fan-out; subscribers own the wire contract.
builder.Services.AddSingleton<IEventBus, InMemoryEventBus>();
builder.Services.AddSingleton<HubBroadcastSubscriber>();
builder.Services.AddSingleton<MetricsSubscriber>();
builder.Services.AddSingleton<LogStreamSubscriber>();
builder.Services.AddSingleton<ShowAgentCommandSubscriber>();

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    await DevelopmentWorkspaceBootstrap.EnsureAsync(
        app.Services.GetRequiredService<IWorkspaceAccessStore>(),
        app.Configuration);
}

app.UseCors("NuottiCors");
app.UseRateLimiter();
app.UseMiddleware<Nuotti.Backend.Middleware.CorrelationIdMiddleware>();
app.UseMiddleware<ProblemHandlingMiddleware>();
if (app.Environment.IsDevelopment())
{
    // Compatibility surfaces predate Workspace authentication and are deliberately local-only.
    // Deployed environments expose only Workspace-scoped mutation/recovery routes.
    app.MapPhaseEndpoints();
    app.MapApiEndpoints();
    app.MapHub<QuizHub>("/hub").RequireCors("NuottiCors");
    app.MapHub<LogHub>("/log").RequireCors("NuottiCors");
}
app.MapHub<WorkspaceHub>(app.Environment.IsDevelopment() ? "/workspace-hub" : "/hub")
    .RequireCors("NuottiCors");
app.MapHealthEndpoints();
if (app.Environment.IsDevelopment()) app.MapStatusEndpoints();
if (app.Environment.IsDevelopment())
{
    app.MapAudienceCatalogEndpoints();
    app.MapParticipantEndpoints();
    app.MapAudienceAnswerStatusEndpoints();
}
app.MapMetricsEndpoints();
app.MapAboutEndpoints();
app.MapTimeEndpoints();
if (app.Environment.IsDevelopment()) app.MapDiagnosticsEndpoints();
app.MapDevEndpoints();
app.MapInfrastructureProofEndpoints();
app.MapWorkspaceEndpoints();
app.MapShowAgentEndpoints();
app.MapPrivateAssetEndpoints();
app.MapSongPackageEndpoints();
app.MapWorkspaceSetlistEndpoints();
app.MapSessionSnapshotEndpoints();
app.MapRecoveryEndpoints();
app.MapGovernanceEndpoints();
app.MapNuottiEndpoints("Nuotti.Backend");

// Force creation of subscribers so they can attach to the bus
_ = app.Services.GetRequiredService<HubBroadcastSubscriber>();
_ = app.Services.GetRequiredService<MetricsSubscriber>();
_ = app.Services.GetRequiredService<LogStreamSubscriber>();
_ = app.Services.GetRequiredService<ShowAgentCommandSubscriber>();
_ = app.Services.GetRequiredService<ISessionStore>(); // Force session store initialization

var productionGovernance = app.Services.GetRequiredService<Nuotti.Backend.Governance.ProductionGovernance>();
productionGovernance.LogLevelSwitch = app.Services.GetService<LogLevelSwitchService>();

var logger = app.Services.GetRequiredService<ILogger<Program>>();

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
