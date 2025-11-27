using System;
using System.Threading.Tasks;
using NUnit.Framework;

namespace Nuotti.Projector.Tests;

[SetUpFixture]
public class PlaywrightSetup
{
    [OneTimeSetUp]
    public async Task GlobalSetup()
    {
        Console.WriteLine("[setup] Installing Playwright browsers...");
        
        try
        {
            // Install Playwright browsers
            var exitCode = Microsoft.Playwright.Program.Main(new[] { "install" });
            if (exitCode != 0)
            {
                Console.WriteLine($"[setup] WARNING: Playwright install returned exit code {exitCode}");
            }
            else
            {
                Console.WriteLine("[setup] Playwright browsers installed successfully");
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[setup] Error installing Playwright browsers: {ex.Message}");
            // Don't fail the setup, tests might still work with existing browsers
        }
        
        Console.WriteLine("[setup] Global test setup completed");
    }
    
    [OneTimeTearDown]
    public void GlobalTeardown()
    {
        Console.WriteLine("[setup] Global test teardown completed");
    }
}
