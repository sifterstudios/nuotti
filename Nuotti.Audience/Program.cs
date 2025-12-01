using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;
using MudBlazor.Services;
using Nuotti.Audience;
using Nuotti.Audience.Services;
using OpenTelemetry.Exporter;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;
using System.Diagnostics;
using Serilog;
using Serilog.Formatting.Json;
using Serilog.Events;
var builder = WebAssemblyHostBuilder.CreateDefault(args);

// Configure structured logging for Blazor WASM
// Note: In WASM, console logging goes to browser console
var logLevel = builder.Configuration["Logging:LogLevel:Default"]
    ?? Environment.GetEnvironmentVariable("NUOTTI_LOG_LEVEL")
    ?? "Information";

if (!Enum.TryParse<LogEventLevel>(logLevel, ignoreCase: true, out var minLevel))
{
    minLevel = LogEventLevel.Information;
}

Log.Logger = new LoggerConfiguration()
    .MinimumLevel.Is(minLevel)
    .MinimumLevel.Override("Microsoft", LogEventLevel.Warning)
    .MinimumLevel.Override("System", LogEventLevel.Warning)
    .Enrich.WithProperty("service", "Nuotti.Audience")
    .Enrich.FromLogContext()
    .WriteTo.Console(new JsonFormatter(renderMessage: true)) // In WASM, Console goes to browser console
    .CreateLogger();

builder.Logging.ClearProviders();
builder.Logging.AddSerilog();
builder.RootComponents.Add<App>("#app");
builder.RootComponents.Add<HeadOutlet>("head::after");

builder.Services.AddScoped(sp => new HttpClient
{
    BaseAddress = new Uri(builder.HostEnvironment.BaseAddress)
});

builder.Services.AddMudServices();

// AudienceHubClient depends on HttpClient (scoped), so it must be scoped too
builder.Services.AddScoped<AudienceHubClient>();

// Theme service for light/dark mode
builder.Services.AddScoped<ThemeService>();

// Feedback service for haptics and animations
builder.Services.AddScoped<FeedbackService>();

// Name validation service
builder.Services.AddSingleton<NameValidationService>();

// OpenTelemetry: configure tracing/logging for Blazor WASM
// Note: Use HTTP/protobuf exporter; endpoint can be provided via appsettings.json or environment-equivalent
var serviceName = "Nuotti.Audience";

// Logging -> OpenTelemetry
builder.Logging.AddOpenTelemetry(logging =>
{
    logging.IncludeFormattedMessage = true;
    logging.IncludeScopes = true;
});

// Tracing/Metrics
var otlpEndpoint = builder.Configuration["OTEL_EXPORTER_OTLP_ENDPOINT"]; // e.g., https://localhost:4318
var otlpHeaders = builder.Configuration["OTEL_EXPORTER_OTLP_HEADERS"];    // e.g., Authorization=Bearer%20xyz

builder.Services.AddSingleton(new ActivitySource(serviceName));

var otelBuilder = builder.Services.AddOpenTelemetry()
    .ConfigureResource(rb => rb.AddService(serviceName))
    .WithTracing(tracing =>
    {
        tracing
            .AddSource(serviceName)
            .AddHttpClientInstrumentation();

        if (!string.IsNullOrWhiteSpace(otlpEndpoint))
        {
            tracing.AddOtlpExporter(o =>
            {
                o.Endpoint = new Uri(otlpEndpoint);
                o.Protocol = OtlpExportProtocol.HttpProtobuf; // Browser-safe
                if (!string.IsNullOrWhiteSpace(otlpHeaders))
                {
                    o.Headers = otlpHeaders; // comma-separated key=value
                }
            });
        }
    })
    .WithMetrics(metrics =>
    {
        metrics.AddRuntimeInstrumentation();
        if (!string.IsNullOrWhiteSpace(otlpEndpoint))
        {
            metrics.AddOtlpExporter(o =>
            {
                o.Endpoint = new Uri(otlpEndpoint);
                o.Protocol = OtlpExportProtocol.HttpProtobuf;
                if (!string.IsNullOrWhiteSpace(otlpHeaders))
                {
                    o.Headers = otlpHeaders;
                }
            });
        }
    });

Log.Information("Audience starting");

await builder.Build().RunAsync();
