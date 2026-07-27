using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Serilog.Events;

namespace ServiceDefaults;

/// <summary>
/// Startup composition for Nuotti processes.
/// </summary>
/// <remarks>
/// The processes are not all the same kind of host, so this offers one entry point per family
/// rather than pretending a single call fits everything:
///
/// <list type="bullet">
/// <item>ASP.NET hosts (Backend, Performer) — <see cref="AddNuottiWebHost{TBuilder}"/> plus
/// <see cref="MapNuottiEndpoints"/>: service defaults, structured logging, health endpoints, and the
/// startup banner.</item>
/// <item>Plain processes (AudioEngine console, Projector desktop, SimKit console) —
/// <see cref="ConfigureNuottiProcess"/>: structured logging and the startup banner. No HTTP
/// endpoints, because these hosts have none to map.</item>
/// <item>The WASM client (Audience) can share neither: WebAssemblyHostBuilder is not an
/// IHostApplicationBuilder, a browser cannot write log files, and a static client has no health
/// endpoint. It shares <see cref="ResolveLogLevel"/> and <see cref="VersionInfo"/> only.</item>
/// </list>
///
/// Nothing here is a no-op for any caller: a capability a host cannot use is not offered to it.
/// </remarks>
public static class NuottiHost
{
    /// <summary>
    /// Service defaults (service discovery, resilience, health checks, OpenTelemetry) plus
    /// structured logging, for an ASP.NET host.
    /// </summary>
    public static TBuilder AddNuottiWebHost<TBuilder>(this TBuilder builder, bool enableFileSink = true)
        where TBuilder : IHostApplicationBuilder
    {
        builder.AddServiceDefaults();
        builder.ConfigureStructuredLogging(enableFileSink);
        return builder;
    }

    /// <summary>
    /// The shared HTTP surface every Nuotti web host exposes, and the startup banner.
    /// </summary>
    public static WebApplication MapNuottiEndpoints(this WebApplication app, string serviceName)
    {
        app.MapDefaultEndpoints();
        LogStartup(app.Services.GetRequiredService<ILoggerFactory>(), serviceName, app.Configuration);
        return app;
    }

    /// <summary>
    /// Structured logging and the startup banner for a process that is not an ASP.NET host.
    /// </summary>
    public static void ConfigureNuottiProcess(
        string serviceName,
        IConfiguration? configuration = null,
        bool enableFileSink = true)
    {
        Microsoft.Extensions.Hosting.LoggingExtensions.ConfigureStructuredLogging(
            serviceName, configuration, enableFileSink);
        LogStartup(serviceName, configuration);
    }

    /// <summary>
    /// The minimum log level, from "Logging:LogLevel:Default" then NUOTTI_LOG_LEVEL, defaulting to
    /// Information. Shared with the WASM client, which configures its own Serilog.
    /// </summary>
    public static LogEventLevel ResolveLogLevel(IConfiguration? configuration = null)
    {
        var configured = configuration?["Logging:LogLevel:Default"]
                         ?? Environment.GetEnvironmentVariable("NUOTTI_LOG_LEVEL")
                         ?? "Information";

        return Enum.TryParse<LogEventLevel>(configured, ignoreCase: true, out var level)
            ? level
            : LogEventLevel.Information;
    }

    /// <summary>
    /// The startup banner: service, version, build provenance, runtime and enabled feature flags.
    /// Was copy-pasted into four Program.cs files, each logging a slightly different subset.
    /// </summary>
    static void LogStartup(ILoggerFactory loggerFactory, string serviceName, IConfiguration? configuration)
    {
        var version = VersionInfo.GetVersionInfo(serviceName);
        var enabled = EnabledFeatures(configuration);

        loggerFactory.CreateLogger(serviceName).LogInformation(
            "{Service} started. Version={Version}, GitCommit={GitCommit}, BuildTime={BuildTime}, Runtime={Runtime}, EnabledFeatures=[{EnabledFeatures}]",
            version.Service, version.Version, version.GitCommit, version.BuildTime, version.Runtime, enabled);
    }

    static void LogStartup(string serviceName, IConfiguration? configuration)
    {
        var version = VersionInfo.GetVersionInfo(serviceName);
        var enabled = EnabledFeatures(configuration);

        // Non-host processes have no ILoggerFactory at this point; Serilog's static logger is
        // already configured by the call above.
        Serilog.Log.Information(
            "{Service} started. Version={Version}, GitCommit={GitCommit}, BuildTime={BuildTime}, Runtime={Runtime}, EnabledFeatures=[{EnabledFeatures}]",
            version.Service, version.Version, version.GitCommit, version.BuildTime, version.Runtime, enabled);
    }

    static string EnabledFeatures(IConfiguration? configuration)
    {
        if (configuration is null) return string.Empty;

        var enabled = FeatureFlags.GetAll(configuration)
            .Where(f => f.Value)
            .Select(f => f.Key);

        return string.Join(", ", enabled);
    }
}
