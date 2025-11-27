using Avalonia;
using System;
using Serilog;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Configuration;

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
        LoggingExtensions.ConfigureStructuredLogging("Nuotti.Projector", config);
        
        Log.Information("Projector starting. Service={Service}, Version={Version}", "Nuotti.Projector", "1.0.0");
        
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