using Avalonia;
using System;
using Serilog;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Configuration;
using HostingLoggingExtensions = Microsoft.Extensions.Hosting.LoggingExtensions;

namespace Nuotti.Projector;

class Program
{
    // Initialization code. Don't use any Avalonia, third-party APIs or any
    // SynchronizationContext-reliant code before AppMain is called: things aren't initialized
    // yet and stuff might break.
    [STAThread]
    public static void Main(string[] args)
    {
        // Configure structured logging for Avalonia app
        var config = new ConfigurationBuilder()
            .AddEnvironmentVariables(prefix: "NUOTTI_")
            .Build();
        Microsoft.Extensions.Hosting.LoggingExtensions.ConfigureStructuredLogging("Nuotti.Projector", config);
        
        var versionInfo = ServiceDefaults.VersionInfo.GetVersionInfo("Nuotti.Projector");
        Log.Information("Projector starting. Service={Service}, Version={Version}, GitCommit={GitCommit}, BuildTime={BuildTime}, Runtime={Runtime}", 
            versionInfo.Service, versionInfo.Version, versionInfo.GitCommit, versionInfo.BuildTime, versionInfo.Runtime);
        
        try
        {
            BuildAvaloniaApp()
                .StartWithClassicDesktopLifetime(args);
        }
        finally
        {
            Log.CloseAndFlush();
        }
    }

    // Avalonia configuration, don't remove; also used by visual designer.
    public static AppBuilder BuildAvaloniaApp()
        => AppBuilder.Configure<App>()
            .UsePlatformDetect()
            .WithInterFont()
            .LogToTrace(); // Keep Avalonia's trace logging for framework diagnostics
}