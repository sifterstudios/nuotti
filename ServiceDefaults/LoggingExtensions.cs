using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Serilog;
using Serilog.Events;
using Serilog.Formatting.Json;

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
    /// </summary>
    public static TBuilder ConfigureStructuredLogging<TBuilder>(this TBuilder builder) where TBuilder : IHostApplicationBuilder
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

        // Configure Serilog
        Log.Logger = new LoggerConfiguration()
            .MinimumLevel.Is(minLevel)
            .MinimumLevel.Override("Microsoft", LogEventLevel.Warning)
            .MinimumLevel.Override("Microsoft.AspNetCore", LogEventLevel.Warning)
            .MinimumLevel.Override("System", LogEventLevel.Warning)
            .Enrich.WithProperty("service", serviceName)
            .Enrich.FromLogContext() // Enables LogContext.PushProperty for correlation IDs
            .WriteTo.Console(new JsonFormatter(renderMessage: true))
            .CreateLogger();

        builder.Logging.ClearProviders();
        builder.Logging.AddSerilog();

        return builder;
    }

    /// <summary>
    /// Configures Serilog for console applications (non-web hosts).
    /// </summary>
    public static void ConfigureStructuredLogging(string serviceName, IConfiguration? configuration = null)
    {
        var logLevel = configuration?["Logging:LogLevel:Default"] 
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
            .Enrich.WithProperty("service", serviceName)
            .Enrich.FromLogContext()
            .WriteTo.Console(new JsonFormatter(renderMessage: true))
            .CreateLogger();
    }
}

