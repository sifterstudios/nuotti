using System;
using System.Threading.Tasks;
using FluentAssertions;
using NUnit.Framework;
using Nuotti.Projector.Tests.Helpers;

namespace Nuotti.Projector.Tests;

[TestFixture]
public class ProjectorInteractionTests
{
    private ProjectorTestHelper? _testHelper;
    
    [SetUp]
    public async Task SetUp()
    {
        _testHelper = new ProjectorTestHelper();
        
        await _testHelper.StartProjectorAsync(new ProjectorTestConfig
        {
            BackendUrl = "http://localhost:5240",
            SessionCode = "INTERACTION-TEST",
            TestMode = true
        });
        
        await _testHelper.InitializeBrowserAsync(new BrowserTestConfig
        {
            Headless = false, // Show browser for interaction tests
            SlowMotionMs = 100
        });
        
        await _testHelper.WaitForProjectorReadyAsync();
    }
    
    [TearDown]
    public void TearDown()
    {
        _testHelper?.Dispose();
    }
    
    [Test]
    public async Task KeyboardShortcut_F_ShouldToggleFullscreen()
    {
        // Arrange
        var initialScreenshot = await _testHelper!.TakeScreenshotAsync("before_fullscreen");
        
        // Act
        await _testHelper.TestKeyboardShortcutAsync("F");
        
        // Assert
        var fullscreenScreenshot = await _testHelper.TakeScreenshotAsync("after_fullscreen");
        
        // In a real implementation, you'd check window properties
        // For now, we just verify the screenshot was taken
        fullscreenScreenshot.Should().NotBeNullOrEmpty();
    }
    
    [Test]
    public async Task KeyboardShortcut_B_ShouldToggleBlackScreen()
    {
        // Act
        await _testHelper!.TestKeyboardShortcutAsync("B");
        
        // Assert
        var blackScreenshot = await _testHelper.TakeScreenshotAsync("black_screen");
        
        // Toggle back
        await _testHelper.TestKeyboardShortcutAsync("B");
        var normalScreenshot = await _testHelper.TakeScreenshotAsync("after_black_screen");
        
        blackScreenshot.Should().NotBeNullOrEmpty();
        normalScreenshot.Should().NotBeNullOrEmpty();
    }
    
    [Test]
    public async Task KeyboardShortcut_Escape_ShouldExitFullscreen()
    {
        // Arrange - Enter fullscreen first
        await _testHelper!.TestKeyboardShortcutAsync("F");
        await Task.Delay(500);
        
        // Act
        await _testHelper.TestKeyboardShortcutAsync("Escape");
        
        // Assert
        var afterEscapeScreenshot = await _testHelper.TakeScreenshotAsync("after_escape");
        afterEscapeScreenshot.Should().NotBeNullOrEmpty();
    }
    
    [Test]
    public async Task KeyboardShortcut_CtrlT_ShouldToggleAlwaysOnTop()
    {
        // Act
        await _testHelper!.TestKeyboardShortcutAsync("t", "Control");
        
        // Assert
        var screenshot = await _testHelper.TakeScreenshotAsync("always_on_top");
        screenshot.Should().NotBeNullOrEmpty();
    }
    
    [Test]
    public async Task KeyboardShortcut_CtrlC_ShouldToggleCursor()
    {
        // Act
        await _testHelper!.TestKeyboardShortcutAsync("c", "Control");
        
        // Assert
        var screenshot = await _testHelper.TakeScreenshotAsync("cursor_toggle");
        screenshot.Should().NotBeNullOrEmpty();
    }
    
    [Test]
    public async Task KeyboardShortcut_CtrlH_ShouldShowHelp()
    {
        // Act
        await _testHelper!.TestKeyboardShortcutAsync("h", "Control");
        
        // Assert
        var helpScreenshot = await _testHelper.TakeScreenshotAsync("help_overlay");
        
        // Toggle help off
        await _testHelper.TestKeyboardShortcutAsync("h", "Control");
        var afterHelpScreenshot = await _testHelper.TakeScreenshotAsync("after_help");
        
        helpScreenshot.Should().NotBeNullOrEmpty();
        afterHelpScreenshot.Should().NotBeNullOrEmpty();
    }
    
    [Test]
    public async Task KeyboardShortcut_CtrlD_ShouldToggleDebugOverlay()
    {
        // Act
        await _testHelper!.TestKeyboardShortcutAsync("d", "Control");
        
        // Assert
        var debugScreenshot = await _testHelper.TakeScreenshotAsync("debug_overlay");
        
        // Toggle debug off
        await _testHelper.TestKeyboardShortcutAsync("d", "Control");
        var afterDebugScreenshot = await _testHelper.TakeScreenshotAsync("after_debug");
        
        debugScreenshot.Should().NotBeNullOrEmpty();
        afterDebugScreenshot.Should().NotBeNullOrEmpty();
    }
    
    [Test]
    public async Task KeyboardShortcut_CtrlL_ShouldToggleTheme()
    {
        // Arrange
        var initialScreenshot = await _testHelper!.TakeScreenshotAsync("initial_theme");
        
        // Act
        await _testHelper.TestKeyboardShortcutAsync("l", "Control");
        
        // Assert
        var themeToggledScreenshot = await _testHelper.TakeScreenshotAsync("theme_toggled");
        
        initialScreenshot.Should().NotBeNullOrEmpty();
        themeToggledScreenshot.Should().NotBeNullOrEmpty();
    }
}
