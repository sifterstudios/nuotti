using System;
using System.IO;
using System.Threading.Tasks;
using FluentAssertions;
using NUnit.Framework;
using Nuotti.Projector.Tests.Helpers;
using Nuotti.Projector.Tests.TestData;

namespace Nuotti.Projector.Tests;

[TestFixture]
public class ProjectorVisualTests
{
    private ProjectorTestHelper? _testHelper;
    private string _baselineDirectory = string.Empty;
    
    [OneTimeSetUp]
    public async Task OneTimeSetUp()
    {
        // Install Playwright browsers if needed
        Microsoft.Playwright.Program.Main(new[] { "install" });
        
        _baselineDirectory = Path.Combine(TestContext.CurrentContext.TestDirectory, "Screenshots", "Baselines");
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
            SessionCode = "E2E-TEST",
            TestMode = true
        });
        
        // Initialize browser for visual testing
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
    public async Task LobbyPhase_ShouldDisplayCorrectly()
    {
        // Arrange
        await _testHelper!.SimulateGameStateAsync("Lobby", MockGameStates.CreateLobbyState(3));
        
        // Act & Assert
        var screenshotPath = await _testHelper.TakeScreenshotAsync("lobby_phase");
        var baselinePath = Path.Combine(_baselineDirectory, "lobby_phase_baseline.png");
        
        var isMatch = await _testHelper.CompareScreenshotAsync("lobby_phase", baselinePath);
        isMatch.Should().BeTrue("Lobby phase should match baseline screenshot");
    }
    
    [Test]
    public async Task ReadyPhase_ShouldDisplayCountdown()
    {
        // Arrange
        await _testHelper!.SimulateGameStateAsync("Ready", MockGameStates.CreateReadyState());
        
        // Act & Assert
        var screenshotPath = await _testHelper.TakeScreenshotAsync("ready_phase");
        var baselinePath = Path.Combine(_baselineDirectory, "ready_phase_baseline.png");
        
        var isMatch = await _testHelper.CompareScreenshotAsync("ready_phase", baselinePath);
        isMatch.Should().BeTrue("Ready phase should match baseline screenshot");
    }
    
    [Test]
    public async Task SongIntroPhase_ShouldShowNowPlayingBanner()
    {
        // Arrange
        await _testHelper!.SimulateGameStateAsync("SongIntro", MockGameStates.CreateSongIntroState());
        
        // Act & Assert
        var screenshotPath = await _testHelper.TakeScreenshotAsync("song_intro_phase");
        var baselinePath = Path.Combine(_baselineDirectory, "song_intro_phase_baseline.png");
        
        var isMatch = await _testHelper.CompareScreenshotAsync("song_intro_phase", baselinePath);
        isMatch.Should().BeTrue("Song intro phase should match baseline screenshot");
    }
    
    [Test]
    public async Task HintPhase_ShouldDisplayHintsCorrectly()
    {
        // Arrange
        await _testHelper!.SimulateGameStateAsync("Hint", MockGameStates.CreateHintState(2));
        
        // Act & Assert
        var screenshotPath = await _testHelper.TakeScreenshotAsync("hint_phase");
        var baselinePath = Path.Combine(_baselineDirectory, "hint_phase_baseline.png");
        
        var isMatch = await _testHelper.CompareScreenshotAsync("hint_phase", baselinePath);
        isMatch.Should().BeTrue("Hint phase should match baseline screenshot");
    }
    
    [Test]
    public async Task GuessingPhase_ShouldShow2x2QuestionLayout()
    {
        // Arrange
        await _testHelper!.SimulateGameStateAsync("Guessing", MockGameStates.CreateGuessingState());
        
        // Act & Assert
        var screenshotPath = await _testHelper.TakeScreenshotAsync("guessing_phase");
        var baselinePath = Path.Combine(_baselineDirectory, "guessing_phase_baseline.png");
        
        var isMatch = await _testHelper.CompareScreenshotAsync("guessing_phase", baselinePath);
        isMatch.Should().BeTrue("Guessing phase should show 2x2 question layout");
    }
    
    [Test]
    public async Task GuessingPhase_ShouldShowLiveTallies()
    {
        // Arrange
        await _testHelper!.SimulateGameStateAsync("Guessing", MockGameStates.CreateGuessingState());
        
        // Wait for tally animations
        await Task.Delay(1000);
        
        // Act & Assert
        var screenshotPath = await _testHelper.TakeScreenshotAsync("guessing_phase_tallies");
        var baselinePath = Path.Combine(_baselineDirectory, "guessing_phase_tallies_baseline.png");
        
        var isMatch = await _testHelper.CompareScreenshotAsync("guessing_phase_tallies", baselinePath);
        isMatch.Should().BeTrue("Guessing phase should show live tallies");
    }
    
    [Test]
    public async Task RevealPhase_ShouldHighlightCorrectAnswer()
    {
        // Arrange
        await _testHelper!.SimulateGameStateAsync("Reveal", MockGameStates.CreateRevealState());
        
        // Act & Assert
        var screenshotPath = await _testHelper.TakeScreenshotAsync("reveal_phase");
        var baselinePath = Path.Combine(_baselineDirectory, "reveal_phase_baseline.png");
        
        var isMatch = await _testHelper.CompareScreenshotAsync("reveal_phase", baselinePath);
        isMatch.Should().BeTrue("Reveal phase should highlight correct answer");
    }
    
    [Test]
    public async Task IntermissionPhase_ShouldShowScoreboard()
    {
        // Arrange
        await _testHelper!.SimulateGameStateAsync("Intermission", MockGameStates.CreateIntermissionState());
        
        // Act & Assert
        var screenshotPath = await _testHelper.TakeScreenshotAsync("intermission_phase");
        var baselinePath = Path.Combine(_baselineDirectory, "intermission_phase_baseline.png");
        
        var isMatch = await _testHelper.CompareScreenshotAsync("intermission_phase", baselinePath);
        isMatch.Should().BeTrue("Intermission phase should show scoreboard");
    }
    
    [Test]
    public async Task FinishedPhase_ShouldShowFinalResults()
    {
        // Arrange
        await _testHelper!.SimulateGameStateAsync("Finished", MockGameStates.CreateFinishedState());
        
        // Act & Assert
        var screenshotPath = await _testHelper.TakeScreenshotAsync("finished_phase");
        var baselinePath = Path.Combine(_baselineDirectory, "finished_phase_baseline.png");
        
        var isMatch = await _testHelper.CompareScreenshotAsync("finished_phase", baselinePath);
        isMatch.Should().BeTrue("Finished phase should show final results");
    }
}
