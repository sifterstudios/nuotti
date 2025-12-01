using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Serilog;
using Serilog.Events;
using Serilog.Formatting.Json;
using ServiceDefaults;

namespace Microsoft.Extensions.Hosting;

/// <summary>
/// Shared Serilog configuration for all Nuotti services.
/// Provides structured JSON logging with common fields: service, version, session, role, connectionId.
/// </summary>
public static class LoggingExtensions
{
    /// <summary>
    /// Configures Serilog with JSON console sink for structured logging.
    /// Common fields: service, version, session, role, connectionId (when present).
    /// Log level is configurable via "Logging:LogLevel:Default" config key or NUOTTI_LOG_LEVEL environment variable.
    /// Optionally includes file sink with rotation for services that need log persistence.
    /// </summary>
    public static TBuilder ConfigureStructuredLogging<TBuilder>(this TBuilder builder, bool enableFileSink = false) where TBuilder : IHostApplicationBuilder
    {
        var serviceName = builder.Environment.ApplicationName;

        // Get log level from config or environment variable
        var logLevel = builder.Configuration["Logging:LogLevel:Default"]
            ?? Environment.GetEnvironmentVariable("NUOTTI_LOG_LEVEL")
            ?? "Information";

        if (!Enum.TryParse<LogEventLevel>(logLevel, ignoreCase: true, out var minLevel))
        {
            minLevel = LogEventLevel.Information;
        }

        // Add HttpContextAccessor for correlation ID enricher
        builder.Services.AddHttpContextAccessor();

        // Add log level switch service for dynamic log level changes
        var logLevelSwitchService = new ServiceDefaults.LogLevelSwitchService(minLevel);
        builder.Services.AddSingleton(logLevelSwitchService);

        // Configure Serilog with dynamic level switch and PII redaction
        var loggerConfig = new LoggerConfiguration()
            .MinimumLevel.ControlledBy(logLevelSwitchService.LevelSwitch)
            .MinimumLevel.Override("Microsoft", LogEventLevel.Warning)
            .MinimumLevel.Override("Microsoft.AspNetCore", LogEventLevel.Warning)
            .MinimumLevel.Override("System", LogEventLevel.Warning)
            .Enrich.WithProperty("service", serviceName)
            .Enrich.FromLogContext() // Enables LogContext.PushProperty for correlation IDs
            .Enrich.With(new ServiceDefaults.PiiEnricher()) // Redact PII from log properties
            .WriteTo.Console(new JsonFormatter(renderMessage: true));

        // Add file sink with rotation if enabled
        if (enableFileSink)
        {
            var logDir = LogFileHelper.GetLogDirectory(serviceName);
            var logPath = Path.Combine(logDir, $"{serviceName}-.log");

            loggerConfig.WriteTo.File(
                new JsonFormatter(renderMessage: true),
                logPath,
                rollingInterval: RollingInterval.Day,
                retainedFileCountLimit: 7, // Keep 7 days of logs
                fileSizeLimitBytes: 100_000_000, // 100MB per file
                rollOnFileSizeLimit: true);
        }

        Log.Logger = loggerConfig.CreateLogger();

        builder.Logging.ClearProviders();
        builder.Logging.AddSerilog();

        return builder;
    }

    /// <summary>
    /// Configures Serilog with JSON console sink for structured logging.
    /// Optionally includes file sink with rotation for services that need log persistence.
    /// </summary>
    public static void ConfigureStructuredLogging(string serviceName, IConfiguration? configuration = null, bool enableFileSink = false)
    {
        var logLevel = configuration?["Logging:LogLevel:Default"]
            ?? Environment.GetEnvironmentVariable("NUOTTI_LOG_LEVEL")
            ?? "Information";

        if (!Enum.TryParse<LogEventLevel>(logLevel, ignoreCase: true, out var minLevel))
        {
            minLevel = LogEventLevel.Information;
        }

        var loggerConfig = new LoggerConfiguration()
            .MinimumLevel.Is(minLevel)
            .MinimumLevel.Override("Microsoft", LogEventLevel.Warning)
            .MinimumLevel.Override("System", LogEventLevel.Warning)
            .Enrich.WithProperty("service", serviceName)
            .Enrich.FromLogContext()
            .Enrich.With(new ServiceDefaults.PiiEnricher()) // Redact PII from log properties
            .WriteTo.Console(new JsonFormatter(renderMessage: true));

        // Add file sink with rotation if enabled
        if (enableFileSink)
        {
            var logDir = LogFileHelper.GetLogDirectory(serviceName);
            var logPath = Path.Combine(logDir, $"{serviceName}-.log");

            loggerConfig.WriteTo.File(
                new JsonFormatter(renderMessage: true),
                logPath,
                rollingInterval: RollingInterval.Day,
                retainedFileCountLimit: 7, // Keep 7 days of logs
                fileSizeLimitBytes: 100_000_000, // 100MB per file
                rollOnFileSizeLimit: true);
        }

        Log.Logger = loggerConfig.CreateLogger();
    }
}

