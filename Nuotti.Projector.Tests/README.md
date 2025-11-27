# Nuotti Projector E2E Tests

This project contains end-to-end (E2E) visual regression tests for the Nuotti Projector application using Playwright.

## Test Categories

### 1. Visual Tests (`ProjectorVisualTests.cs`)
- **Purpose**: Verify that each game phase renders correctly
- **Method**: Screenshot comparison against baseline images
- **Coverage**: All game phases (Lobby, Ready, SongIntro, Hint, Guessing, Reveal, Intermission, Finished)

### 2. Interaction Tests (`ProjectorInteractionTests.cs`)
- **Purpose**: Test keyboard shortcuts and user interactions
- **Method**: Simulate key presses and verify UI responses
- **Coverage**: All F14 keyboard shortcuts (F, B, Esc, Ctrl+T, Ctrl+C, Ctrl+H, Ctrl+D, Ctrl+L)

### 3. Performance Tests (`ProjectorPerformanceTests.cs`)
- **Purpose**: Ensure the projector meets performance requirements
- **Method**: Time critical operations and verify they complete within acceptable limits
- **Coverage**: Startup time, phase transitions, tally updates, screenshot capture, keyboard responsiveness

## Setup Requirements

### Prerequisites
1. **.NET 9.0 SDK** - Required for building and running tests
2. **Playwright Browsers** - Automatically installed during first test run
3. **Nuotti Backend** - Should be running on `http://localhost:5240` for full integration tests

### Installation
```bash
# Navigate to the test project
cd Nuotti.Projector.Tests

# Restore NuGet packages
dotnet restore

# Install Playwright browsers (done automatically on first run)
dotnet run --project . -- playwright install
```

## Running Tests

### Run All Tests
```bash
dotnet test
```

### Run Specific Test Categories
```bash
# Visual tests only
dotnet test --filter "Category=Visual"

# Performance tests only
dotnet test --filter "Category=Performance"

# Interaction tests only
dotnet test --filter "Category=Interaction"
```

### Run Individual Tests
```bash
# Run a specific test
dotnet test --filter "TestName~LobbyPhase_ShouldDisplayCorrectly"
```

### Debug Mode (Show Browser)
To run tests with visible browser windows for debugging:
1. Edit the test file
2. Change `Headless = true` to `Headless = false` in `BrowserTestConfig`
3. Run the tests

## Test Configuration

### ProjectorTestConfig
- `BackendUrl`: URL of the Nuotti backend (default: `http://localhost:5240`)
- `SessionCode`: Session code for testing (default: `"test"`)
- `TestMode`: Enable test-specific features (default: `true`)
- `StartupDelayMs`: Time to wait for projector startup (default: `3000ms`)

### BrowserTestConfig
- `Headless`: Run browser in headless mode (default: `true`)
- `SlowMotionMs`: Slow down actions for debugging (default: `0`)
- `ViewportWidth/Height`: Browser viewport size (default: `1920x1080`)

## Screenshot Management

### Baseline Screenshots
- **Location**: `Screenshots/Baselines/`
- **Purpose**: Reference images for visual regression testing
- **Creation**: Automatically created on first test run if missing
- **Updates**: Manually replace when UI changes are intentional

### Test Screenshots
- **Location**: `Screenshots/`
- **Purpose**: Current test run screenshots for comparison
- **Naming**: `{testname}_{timestamp}.png`
- **Cleanup**: Automatically managed by test framework

### CI/CD Integration
For continuous integration, consider:
1. **Artifact Storage**: Save screenshots as build artifacts
2. **Baseline Management**: Store baselines in version control or artifact repository
3. **Diff Reports**: Generate visual diff reports for failed comparisons
4. **Parallel Execution**: Run tests in parallel with different viewport sizes

## Test Data

### Mock Game States (`TestData/MockGameStates.cs`)
Provides realistic test data for all game phases:
- **Players**: Mock player lists with various sizes
- **Songs**: Sample song metadata (Queen - Bohemian Rhapsody)
- **Questions**: Multiple choice questions with options
- **Tallies**: Realistic answer distribution
- **Scoreboard**: Player rankings with score changes

### Customization
To add new test scenarios:
1. Create new mock data in `MockGameStates.cs`
2. Add corresponding test methods in test classes
3. Update baseline screenshots as needed

## Troubleshooting

### Common Issues

#### 1. Projector Not Starting
- **Symptom**: `TimeoutException` during `WaitForProjectorReadyAsync`
- **Solutions**:
  - Ensure Nuotti.Projector builds successfully
  - Check that no other projector instances are running
  - Verify backend is accessible at configured URL
  - Increase `StartupDelayMs` in test config

#### 2. Screenshot Comparison Failures
- **Symptom**: Visual tests fail with screenshot mismatches
- **Solutions**:
  - Check if UI changes are intentional (update baselines)
  - Verify font rendering consistency across environments
  - Ensure consistent viewport sizes
  - Check for timing issues (add delays before screenshots)

#### 3. Playwright Browser Issues
- **Symptom**: Browser fails to launch or connect
- **Solutions**:
  - Run `dotnet run -- playwright install` manually
  - Check system requirements for Playwright
  - Try different browser types (Chromium, Firefox, WebKit)
  - Verify no conflicting browser processes

#### 4. Performance Test Failures
- **Symptom**: Performance tests exceed time limits
- **Solutions**:
  - Run tests on faster hardware
  - Adjust performance thresholds in test code
  - Check for system resource contention
  - Profile projector application for bottlenecks

### Debug Tips
1. **Enable Verbose Logging**: Set environment variable `DEBUG=pw:*`
2. **Visual Debugging**: Set `Headless = false` and `SlowMotionMs = 1000`
3. **Screenshot Everything**: Add extra screenshot calls in failing tests
4. **Process Monitoring**: Check projector process status during tests

## Contributing

### Adding New Tests
1. **Visual Tests**: Add new test methods to `ProjectorVisualTests.cs`
2. **Interaction Tests**: Add keyboard/mouse tests to `ProjectorInteractionTests.cs`
3. **Performance Tests**: Add timing tests to `ProjectorPerformanceTests.cs`

### Test Naming Convention
- Use descriptive names: `{Phase}_{Action}_Should{ExpectedResult}`
- Examples: `LobbyPhase_ShouldDisplayCorrectly`, `KeyboardShortcut_F_ShouldToggleFullscreen`

### Baseline Management
- **New Features**: Create new baselines for new UI elements
- **UI Changes**: Update existing baselines when changes are intentional
- **Cross-Platform**: Consider separate baselines for different operating systems

## Integration with CI/CD

### GitHub Actions Example
```yaml
name: E2E Tests
on: [push, pull_request]
jobs:
  e2e-tests:
    runs-on: ubuntu-latest
    steps:
      - uses: actions/checkout@v3
      - uses: actions/setup-dotnet@v3
        with:
          dotnet-version: '9.0.x'
      - name: Run E2E Tests
        run: |
          cd Nuotti.Projector.Tests
          dotnet test --logger trx --results-directory TestResults
      - name: Upload Screenshots
        uses: actions/upload-artifact@v3
        if: always()
        with:
          name: test-screenshots
          path: Nuotti.Projector.Tests/Screenshots/
```

This comprehensive E2E testing setup ensures the Nuotti Projector maintains visual consistency and performance standards across all supported scenarios.
