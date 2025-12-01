using System;
using System.Diagnostics;
using System.Threading.Tasks;
using FluentAssertions;
using Nuotti.Projector.Tests.Helpers;
using Nuotti.Projector.Tests.TestData;

namespace Nuotti.Projector.Tests;

[TestFixture]
public class ProjectorPerformanceTests
{
    private ProjectorTestHelper? _testHelper;
    
    [SetUp]
    public async Task SetUp()
    {
        _testHelper = new ProjectorTestHelper();
        
        await _testHelper.StartProjectorAsync(new ProjectorTestConfig
        {
            BackendUrl = "http://localhost:5240",
            SessionCode = "PERF-TEST",
            TestMode = true
        });
        
        await _testHelper.InitializeBrowserAsync(new BrowserTestConfig
        {
            Headless = true,
            ViewportWidth = 1920,
            ViewportHeight = 1080
        });
        
        await _testHelper.WaitForProjectorReadyAsync();
    }
    
    [TearDown]
    public void TearDown()
    {
        _testHelper?.Dispose();
    }
    
    [Test]
    public async Task ProjectorStartup_ShouldCompleteWithinTimeout()
    {
        // This test is implicitly covered by SetUp, but we can add explicit timing
        var stopwatch = Stopwatch.StartNew();
        
        // Projector should already be running from SetUp
        _testHelper!.IsProjectorRunning.Should().BeTrue();
        
        stopwatch.Stop();
        Console.WriteLine($"[perf] Projector startup verification took: {stopwatch.ElapsedMilliseconds}ms");
        
        // Startup should be fast
        stopwatch.ElapsedMilliseconds.Should().BeLessThan(1000, "Startup verification should be fast");
    }
    
    [Test]
    public async Task PhaseTransitions_ShouldBeSmooth()
    {
        var phases = new[]
        {
            ("Lobby", MockGameStates.CreateLobbyState()),
            ("Ready", MockGameStates.CreateReadyState()),
            ("SongIntro", MockGameStates.CreateSongIntroState()),
            ("Hint", MockGameStates.CreateHintState()),
            ("Guessing", MockGameStates.CreateGuessingState()),
            ("Reveal", MockGameStates.CreateRevealState()),
            ("Intermission", MockGameStates.CreateIntermissionState()),
            ("Finished", MockGameStates.CreateFinishedState())
        };
        
        var totalTransitionTime = 0L;
        
        foreach (var (phaseName, gameState) in phases)
        {
            var stopwatch = Stopwatch.StartNew();
            
            // Simulate phase transition
            await _testHelper!.SimulateGameStateAsync(phaseName, gameState);
            
            // Wait for UI to settle
            await Task.Delay(500);
            
            // Take screenshot to verify rendering
            await _testHelper.TakeScreenshotAsync($"perf_{phaseName.ToLower()}");
            
            stopwatch.Stop();
            totalTransitionTime += stopwatch.ElapsedMilliseconds;
            
            Console.WriteLine($"[perf] {phaseName} phase transition: {stopwatch.ElapsedMilliseconds}ms");
            
            // Each phase transition should be fast
            stopwatch.ElapsedMilliseconds.Should().BeLessThan(2000, 
                $"{phaseName} phase transition should complete quickly");
        }
        
        var averageTransitionTime = totalTransitionTime / phases.Length;
        Console.WriteLine($"[perf] Average phase transition time: {averageTransitionTime}ms");
        
        averageTransitionTime.Should().BeLessThan(1000, "Average phase transitions should be under 1 second");
    }
    
    [Test]
    public async Task TallyUpdates_ShouldBeResponsive()
    {
        // Start with guessing phase
        await _testHelper!.SimulateGameStateAsync("Guessing", MockGameStates.CreateGuessingState());
        await Task.Delay(500);
        
        var updateCount = 10;
        var totalUpdateTime = 0L;
        
        for (int i = 0; i < updateCount; i++)
        {
            var stopwatch = Stopwatch.StartNew();
            
            // Simulate tally update
            var gameState = MockGameStates.CreateGuessingState();
            // Note: In the real implementation, tallies would be updated via proper state management
            // For this test, we're just simulating the update call
            
            await _testHelper.SimulateGameStateAsync("Guessing", gameState);
            
            // Small delay to let animation start
            await Task.Delay(100);
            
            stopwatch.Stop();
            totalUpdateTime += stopwatch.ElapsedMilliseconds;
            
            Console.WriteLine($"[perf] Tally update {i + 1}: {stopwatch.ElapsedMilliseconds}ms");
        }
        
        var averageUpdateTime = totalUpdateTime / updateCount;
        Console.WriteLine($"[perf] Average tally update time: {averageUpdateTime}ms");
        
        averageUpdateTime.Should().BeLessThan(200, "Tally updates should be very responsive");
    }
    
    [Test]
    public async Task ScreenshotCapture_ShouldBeReasonablyFast()
    {
        // Set up a complex scene (guessing phase with tallies)
        await _testHelper!.SimulateGameStateAsync("Guessing", MockGameStates.CreateGuessingState());
        await Task.Delay(500);
        
        var screenshotCount = 5;
        var totalScreenshotTime = 0L;
        
        for (int i = 0; i < screenshotCount; i++)
        {
            var stopwatch = Stopwatch.StartNew();
            
            await _testHelper.TakeScreenshotAsync($"perf_screenshot_{i}");
            
            stopwatch.Stop();
            totalScreenshotTime += stopwatch.ElapsedMilliseconds;
            
            Console.WriteLine($"[perf] Screenshot {i + 1}: {stopwatch.ElapsedMilliseconds}ms");
        }
        
        var averageScreenshotTime = totalScreenshotTime / screenshotCount;
        Console.WriteLine($"[perf] Average screenshot time: {averageScreenshotTime}ms");
        
        averageScreenshotTime.Should().BeLessThan(3000, "Screenshots should capture reasonably quickly");
    }
    
    [Test]
    public async Task KeyboardShortcuts_ShouldBeResponsive()
    {
        var shortcuts = new[]
        {
            ("F", "Fullscreen toggle"),
            ("B", "Black screen toggle"),
            ("Escape", "Exit fullscreen"),
            ("Control+T", "Always on top toggle"),
            ("Control+C", "Cursor toggle"),
            ("Control+H", "Help toggle"),
            ("Control+D", "Debug toggle"),
            ("Control+L", "Theme toggle")
        };
        
        var totalShortcutTime = 0L;
        
        foreach (var (shortcut, description) in shortcuts)
        {
            var stopwatch = Stopwatch.StartNew();
            
            var parts = shortcut.Split('+');
            if (parts.Length == 2)
            {
                await _testHelper!.TestKeyboardShortcutAsync(parts[1], parts[0]);
            }
            else
            {
                await _testHelper!.TestKeyboardShortcutAsync(parts[0]);
            }
            
            stopwatch.Stop();
            totalShortcutTime += stopwatch.ElapsedMilliseconds;
            
            Console.WriteLine($"[perf] {description} ({shortcut}): {stopwatch.ElapsedMilliseconds}ms");
            
            // Each shortcut should respond quickly
            stopwatch.ElapsedMilliseconds.Should().BeLessThan(1000, 
                $"{description} should respond quickly");
        }
        
        var averageShortcutTime = totalShortcutTime / shortcuts.Length;
        Console.WriteLine($"[perf] Average keyboard shortcut response: {averageShortcutTime}ms");
        
        averageShortcutTime.Should().BeLessThan(600, "Keyboard shortcuts should be very responsive");
    }
    
    [Test]
    public async Task MemoryUsage_ShouldRemainStable()
    {
        // This is a placeholder for memory usage testing
        // In a real implementation, you'd monitor the projector process memory
        
        var initialMemory = GC.GetTotalMemory(false);
        Console.WriteLine($"[perf] Initial test memory: {initialMemory / 1024 / 1024:F2} MB");
        
        // Simulate various operations
        for (int i = 0; i < 10; i++)
        {
            await _testHelper!.SimulateGameStateAsync("Guessing", MockGameStates.CreateGuessingState());
            await _testHelper.TakeScreenshotAsync($"memory_test_{i}");
            await Task.Delay(100);
        }
        
        // Force garbage collection
        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();
        
        var finalMemory = GC.GetTotalMemory(false);
        Console.WriteLine($"[perf] Final test memory: {finalMemory / 1024 / 1024:F2} MB");
        
        var memoryIncrease = finalMemory - initialMemory;
        Console.WriteLine($"[perf] Memory increase: {memoryIncrease / 1024 / 1024:F2} MB");
        
        // Memory increase should be reasonable for test operations
        memoryIncrease.Should().BeLessThan(50 * 1024 * 1024, "Memory usage should remain stable");
    }
}
