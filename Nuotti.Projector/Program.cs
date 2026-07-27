using Avalonia;
using Microsoft.Extensions.Configuration;
using Serilog;
using ServiceDefaults;
using System;
namespace Nuotti.Projector;

class Program
{
    // Initialization code. Don't use any Avalonia, third-party APIs or any
    // SynchronizationContext-reliant code before AppMain is called: things aren't initialized
    // yet and stuff might break.
    [STAThread]
    public static void Main(string[] args)
    {
        // Early console output to verify the process is starting
        Console.WriteLine("[Projector] Main() called");
        Console.WriteLine($"[Projector] Args: {string.Join(" ", args)}");
        Console.WriteLine($"[Projector] Working Directory: {Environment.CurrentDirectory}");

        try
        {
            Console.WriteLine("[Projector] Configuring logging...");
            var config = new ConfigurationBuilder()
                .AddEnvironmentVariables(prefix: "NUOTTI_")
                .Build();
            NuottiHost.ConfigureNuottiProcess("Nuotti.Projector", config);
            Console.WriteLine("[Projector] Logging configured");

            Console.WriteLine("[Projector] Building Avalonia app...");
            var appBuilder = BuildAvaloniaApp();
            Console.WriteLine("[Projector] Starting desktop lifetime...");
            appBuilder.StartWithClassicDesktopLifetime(args);
            Console.WriteLine("[Projector] Desktop lifetime started");
        }
        catch (Exception ex)
        {
            // Catch any exceptions and write to console before logging might be available
            Console.WriteLine($"[Projector] FATAL ERROR: {ex.GetType().Name}: {ex.Message}");
            Console.WriteLine($"[Projector] Stack trace: {ex.StackTrace}");
            if (ex.InnerException != null)
            {
                Console.WriteLine($"[Projector] Inner exception: {ex.InnerException.GetType().Name}: {ex.InnerException.Message}");
            }

            // Try to log if possible
            try
            {
                Log.Error(ex, "Fatal error starting Projector");
            }
            catch { }

            // Re-throw so the process exits with error code
            throw;
        }
        finally
        {
            Console.WriteLine("[Projector] Cleaning up...");
            try
            {
                Log.CloseAndFlush();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[Projector] Error closing log: {ex.Message}");
            }
        }
    }

    // Avalonia configuration, don't remove; also used by visual designer.
    public static AppBuilder BuildAvaloniaApp()
        => AppBuilder.Configure<App>()
            .UsePlatformDetect()
            .WithInterFont()
            .LogToTrace(); // Keep Avalonia's trace logging for framework diagnostics
}
