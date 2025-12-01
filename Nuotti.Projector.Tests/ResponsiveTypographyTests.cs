using System;
using System.IO;
using System.Threading.Tasks;
using FluentAssertions;
using NUnit.Framework;
using Nuotti.Projector.Tests.Helpers;
using Nuotti.Projector.Tests.TestData;

namespace Nuotti.Projector.Tests;

/// <summary>
/// Visual regression tests for responsive typography across different resolutions.
/// Verifies that text scales appropriately and fits within safe areas at various viewport sizes.
/// </summary>
[TestFixture]
public class ResponsiveTypographyTests
{
    private ProjectorTestHelper? _testHelper;
    private string _baselineDirectory = string.Empty;
    
    // Test resolutions: 720p, 1080p, 4K, and common mobile DPIs
    private readonly (int Width, int Height, string Name)[] _testResolutions = new[]
    {
        (1280, 720, "720p"),      // HD
        (1920, 1080, "1080p"),    // Full HD
        (3840, 2160, "4K"),       // Ultra HD
        (375, 667, "iPhoneSE"),   // iPhone SE
        (390, 844, "iPhone12"),   // iPhone 12
        (428, 926, "iPhone13Pro") // iPhone 13 Pro Max
    };
    
    [OneTimeSetUp]
    public async Task OneTimeSetUp()
    {
        // Install Playwright browsers if needed
        Microsoft.Playwright.Program.Main(new[] { "install" });
        
        _baselineDirectory = Path.Combine(TestContext.CurrentContext.TestDirectory, "Screenshots", "ResponsiveTypography");
        Directory.CreateDirectory(_baselineDirectory);
    }
    
    [SetUp]
    public async Task SetUp()
    {
        _testHelper = new ProjectorTestHelper();
        
        // Start projector in test mode
        await _testHelper.StartProjectorAsync(new ProjectorTestConfig
        {
            BackendUrl = "http://localhost:5240",
            SessionCode = "RESPONSIVE-TEST",
            TestMode = true
        });
        
        await _testHelper.WaitForProjectorReadyAsync();
    }
    
    [TearDown]
    public void TearDown()
    {
        _testHelper?.Dispose();
    }
    
    [Test]
    [TestCaseSource(nameof(_testResolutions))]
    public async Task GuessingView_TextFitsWithinSafeArea_AtResolution((int Width, int Height, string Name) resolution)
    {
        // Arrange
        await _testHelper!.InitializeBrowserAsync(new BrowserTestConfig
        {
            Headless = true,
            ViewportWidth = resolution.Width,
            ViewportHeight = resolution.Height
        });
        
        await _testHelper.SimulateGameStateAsync("Guessing", MockGameStates.CreateGuessingState());
        await Task.Delay(500); // Wait for font size calculations
        
        // Act
        var screenshotPath = await _testHelper.TakeScreenshotAsync($"guessing_{resolution.Name}");
        var baselinePath = Path.Combine(_baselineDirectory, $"guessing_{resolution.Name}_baseline.png");
        
        // Assert - Verify no text overflow
        var hasOverflow = await _testHelper.CheckTextOverflowAsync();
        hasOverflow.Should().BeFalse($"Text should not overflow at {resolution.Name} ({resolution.Width}x{resolution.Height})");
        
        // Visual regression check
        var isMatch = await _testHelper.CompareScreenshotAsync($"guessing_{resolution.Name}", baselinePath);
        isMatch.Should().BeTrue($"Guessing view should match baseline at {resolution.Name}");
    }
    
    [Test]
    [TestCaseSource(nameof(_testResolutions))]
    public async Task LobbyView_HeadlineScalesCorrectly_AtResolution((int Width, int Height, string Name) resolution)
    {
        // Arrange
        await _testHelper!.InitializeBrowserAsync(new BrowserTestConfig
        {
            Headless = true,
            ViewportWidth = resolution.Width,
            ViewportHeight = resolution.Height
        });
        
        await _testHelper.SimulateGameStateAsync("Lobby", MockGameStates.CreateLobbyState(3));
        await Task.Delay(500); // Wait for font size calculations
        
        // Act
        var screenshotPath = await _testHelper.TakeScreenshotAsync($"lobby_{resolution.Name}");
        var baselinePath = Path.Combine(_baselineDirectory, $"lobby_{resolution.Name}_baseline.png");
        
        // Assert - Verify headline is within bounds
        var headlineSize = await _testHelper.GetElementFontSizeAsync("WelcomeText");
        headlineSize.Should().BeGreaterOrEqualTo(36, $"Headline should be at least 36px at {resolution.Name}");
        headlineSize.Should().BeLessOrEqualTo(72, $"Headline should be at most 72px at {resolution.Name}");
        
        // Visual regression check
        var isMatch = await _testHelper.CompareScreenshotAsync($"lobby_{resolution.Name}", baselinePath);
        isMatch.Should().BeTrue($"Lobby view should match baseline at {resolution.Name}");
    }
    
    [Test]
    [TestCaseSource(nameof(_testResolutions))]
    public async Task ScoreboardView_TextScalesCorrectly_AtResolution((int Width, int Height, string Name) resolution)
    {
        // Arrange
        await _testHelper!.InitializeBrowserAsync(new BrowserTestConfig
        {
            Headless = true,
            ViewportWidth = resolution.Width,
            ViewportHeight = resolution.Height
        });
        
        await _testHelper.SimulateGameStateAsync("Intermission", MockGameStates.CreateIntermissionState());
        await Task.Delay(500); // Wait for font size calculations
        
        // Act
        var screenshotPath = await _testHelper.TakeScreenshotAsync($"scoreboard_{resolution.Name}");
        var baselinePath = Path.Combine(_baselineDirectory, $"scoreboard_{resolution.Name}_baseline.png");
        
        // Assert - Verify no text overflow
        var hasOverflow = await _testHelper.CheckTextOverflowAsync();
        hasOverflow.Should().BeFalse($"Text should not overflow at {resolution.Name}");
        
        // Visual regression check
        var isMatch = await _testHelper.CompareScreenshotAsync($"scoreboard_{resolution.Name}", baselinePath);
        isMatch.Should().BeTrue($"Scoreboard view should match baseline at {resolution.Name}");
    }
    
    [Test]
    [TestCaseSource(nameof(_testResolutions))]
    public async Task HintView_TextFitsWithinSafeArea_AtResolution((int Width, int Height, string Name) resolution)
    {
        // Arrange
        await _testHelper!.InitializeBrowserAsync(new BrowserTestConfig
        {
            Headless = true,
            ViewportWidth = resolution.Width,
            ViewportHeight = resolution.Height
        });
        
        await _testHelper.SimulateGameStateAsync("Hint", MockGameStates.CreateHintState(2));
        await Task.Delay(500); // Wait for font size calculations
        
        // Act
        var screenshotPath = await _testHelper.TakeScreenshotAsync($"hint_{resolution.Name}");
        var baselinePath = Path.Combine(_baselineDirectory, $"hint_{resolution.Name}_baseline.png");
        
        // Assert - Verify no text overflow
        var hasOverflow = await _testHelper.CheckTextOverflowAsync();
        hasOverflow.Should().BeFalse($"Text should not overflow at {resolution.Name}");
        
        // Visual regression check
        var isMatch = await _testHelper.CompareScreenshotAsync($"hint_{resolution.Name}", baselinePath);
        isMatch.Should().BeTrue($"Hint view should match baseline at {resolution.Name}");
    }
    
    [Test]
    public async Task FontSizes_RespectMinMaxBounds_AcrossAllResolutions()
    {
        // Test that font sizes are always within defined min/max bounds
        
        foreach (var resolution in _testResolutions)
        {
            await _testHelper!.InitializeBrowserAsync(new BrowserTestConfig
            {
                Headless = true,
                ViewportWidth = resolution.Width,
                ViewportHeight = resolution.Height
            });
            
            await _testHelper.SimulateGameStateAsync("Guessing", MockGameStates.CreateGuessingState());
            await Task.Delay(500);
            
            // Check question text
            var questionSize = await _testHelper.GetElementFontSizeAsync("QuestionText");
            questionSize.Should().BeGreaterOrEqualTo(32, $"Question text should be >= 32px at {resolution.Name}");
            questionSize.Should().BeLessOrEqualTo(48, $"Question text should be <= 48px at {resolution.Name}");
            
            // Check option text
            var optionSize = await _testHelper.GetElementFontSizeAsync("OptionAText");
            optionSize.Should().BeGreaterOrEqualTo(18, $"Option text should be >= 18px at {resolution.Name}");
            optionSize.Should().BeLessOrEqualTo(24, $"Option text should be <= 24px at {resolution.Name}");
        }
    }
    
    [Test]
    public async Task NoLayoutShift_AfterFirstPaint()
    {
        // Verify that layout doesn't shift after initial render
        
        await _testHelper!.InitializeBrowserAsync(new BrowserTestConfig
        {
            Headless = true,
            ViewportWidth = 1920,
            ViewportHeight = 1080
        });
        
        await _testHelper.SimulateGameStateAsync("Guessing", MockGameStates.CreateGuessingState());
        
        // Take initial screenshot
        var initialScreenshot = await _testHelper.TakeScreenshotAsync("initial");
        
        // Wait a bit and take another screenshot
        await Task.Delay(1000);
        var laterScreenshot = await _testHelper.TakeScreenshotAsync("later");
        
        // Compare - they should be identical (no layout shift)
        var isMatch = await _testHelper.CompareScreenshotsAsync(initialScreenshot, laterScreenshot);
        isMatch.Should().BeTrue("Layout should not shift after first paint");
    }
}

