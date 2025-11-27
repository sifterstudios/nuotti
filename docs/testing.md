# Testing Guide

This document provides comprehensive information about running and troubleshooting tests in the Nuotti project.

## Table of Contents

- [Test Projects](#test-projects)
- [Running Tests](#running-tests)
- [Test Categories](#test-categories)
- [Code Coverage](#code-coverage)
- [Troubleshooting](#troubleshooting)
- [CI/CD](#cicd)
- [Related Documentation](#related-documentation)

## Test Projects

The Nuotti solution includes several test projects:

### Unit Tests
- **`tests/Nuotti.UnitTests`** - Unit tests for contracts, reducers, and pure functions
- **`Nuotti.Contracts.Tests`** - Contract serialization and reducer tests
- **`Nuotti.AudioEngine.Tests`** - Audio engine unit tests

### Integration Tests
- **`tests/Nuotti.IntegrationTests`** - Integration tests (currently minimal)
- **`Nuotti.Backend.Tests`** - Backend API and SignalR hub integration tests

### End-to-End Tests
- **`tests/Nuotti.E2E`** - End-to-end tests using SimKit and Playwright
- **`Nuotti.Projector.Tests`** - Projector UI tests
- **`Nuotti.Performer.Tests`** - Performer UI tests

### Simulation Tests
- **`Nuotti.SimKit.Tests`** - SimKit actor and orchestration tests

## Running Tests

### Prerequisites

- .NET SDK 10.0 (see `global.json`)
- For E2E tests: Playwright browsers (installed automatically on first run)
- For frontend tests: Node.js 20+ (for linting/formatting checks)

### Run All Tests

```bash
# From repository root
dotnet test
```

### Run Specific Test Project

```bash
# Unit tests
dotnet test tests/Nuotti.UnitTests/Nuotti.UnitTests.csproj

# Integration tests
dotnet test tests/Nuotti.IntegrationTests/Nuotti.IntegrationTests.csproj

# E2E tests
dotnet test tests/Nuotti.E2E/Nuotti.E2E.csproj

# Backend tests
dotnet test Nuotti.Backend.Tests/Nuotti.Backend.Tests.csproj

# Contracts tests
dotnet test Nuotti.Contracts.Tests/Nuotti.Contracts.Tests.csproj

# AudioEngine tests
dotnet test Nuotti.AudioEngine.Tests/Nuotti.AudioEngine.Tests.csproj
```

### Run Tests by Category

```bash
# Run only E2E tests
dotnet test --filter "Category=E2E"

# Run only unit tests
dotnet test --filter "Category=Unit"

# Run only integration tests
dotnet test --filter "Category=Integration"

# Exclude flaky tests
dotnet test --filter "Flaky!=true"
```

### Run Specific Test

```bash
# Run a specific test by name
dotnet test --filter "FullyQualifiedName~Single_Song_Happy_Path_Completes_Successfully"

# Run tests matching a pattern
dotnet test --filter "FullyQualifiedName~GameReducer"
```

### Run with Code Coverage

```bash
# Run with coverage collection
dotnet test --collect:"XPlat Code Coverage" --settings:.runsettings

# View coverage report (requires reportgenerator)
dotnet tool install -g dotnet-reportgenerator-globaltool
reportgenerator -reports:**/coverage.cobertura.xml -targetdir:./CoverageReport -reporttypes:Html
```

### Run with Verbose Output

```bash
dotnet test --verbosity detailed
```

## Test Categories

Tests are organized using xUnit traits:

### Category Traits

- **`[Trait("Category", "Unit")]`** - Fast, isolated unit tests
- **`[Trait("Category", "Integration")]`** - Integration tests requiring backend
- **`[Trait("Category", "E2E")]`** - End-to-end tests with full stack

### Flaky Test Traits

- **`[Trait("Flaky", "true")]`** - Known flaky test (may fail intermittently)
- **`[Trait("Flaky", "false")]`** - Stable test (default)

See [FLAKY_TESTS.md](./FLAKY_TESTS.md) for more information on flaky test mitigation.

## Code Coverage

### Configuration

Code coverage is configured in `.runsettings`:

- **Format**: JSON, Cobertura, OpenCover
- **Exclusions**: Test projects are excluded
- **Threshold**: 80% line coverage (enforced in CI)

### Viewing Coverage

1. Run tests with coverage:
   ```bash
   dotnet test --collect:"XPlat Code Coverage" --settings:.runsettings
   ```

2. Generate HTML report:
   ```bash
   dotnet tool install -g dotnet-reportgenerator-globaltool
   reportgenerator -reports:**/coverage.cobertura.xml -targetdir:./CoverageReport -reporttypes:Html
   ```

3. Open `CoverageReport/index.html` in a browser

### Coverage Thresholds

- **Minimum**: 80% line coverage
- **Target**: 90% line coverage
- CI will fail if coverage drops below threshold

## Troubleshooting

### Common Issues

#### E2E Tests Fail with "PlaywrightException"

**Problem**: Playwright browsers not installed.

**Solution**:
```bash
# Install Playwright browsers
dotnet test tests/Nuotti.E2E/Nuotti.E2E.csproj
# Or manually:
pwsh bin/Debug/net*/playwright.ps1 install
```

#### Tests Fail with "Port Already in Use"

**Problem**: Another process is using the test port.

**Solution**:
```bash
# Find and kill the process (Windows)
netstat -ano | findstr :5240
taskkill /PID <pid> /F

# Or use a different port in test configuration
```

#### Flaky Test Failures

**Problem**: Test fails intermittently.

**Solution**:
1. Check if test uses randomness - add deterministic seed
2. Increase timeouts for E2E tests
3. Check resource cleanup
4. See [FLAKY_TESTS.md](./FLAKY_TESTS.md) for detailed guidance

#### Backend Not Starting in Tests

**Problem**: `WebApplicationFactory` fails to start backend.

**Solution**:
1. Ensure backend project builds successfully
2. Check for missing configuration files
3. Verify port availability
4. Check logs for startup errors

#### SignalR Connection Failures

**Problem**: SignalR hub tests fail to connect.

**Solution**:
1. Verify hub endpoint is correctly configured
2. Check CORS settings
3. Ensure test uses correct base URL
4. Check for firewall/network issues

### Debug Mode

#### Run Tests with Debugger

In Visual Studio or Rider:
1. Set breakpoints in test code
2. Right-click test → "Debug Test"
3. Or use "Debug All Tests"

#### Run E2E Tests with Visible Browser

Edit test file:
```csharp
_browser = await _playwright.Chromium.LaunchAsync(new BrowserTypeLaunchOptions 
{ 
    Headless = false  // Change to false
});
```

#### Enable Detailed Logging

```bash
dotnet test --verbosity detailed --logger:"console;verbosity=detailed"
```

### Test Isolation

Tests should be independent and not share state:

- Each test creates its own session
- Tests use unique session codes (GUID-based)
- Resources are disposed in `IAsyncLifetime.DisposeAsync`
- No static state between tests

## CI/CD

### GitHub Actions

Tests run automatically on:
- Push to `main` branch
- Pull requests to `main`
- Manual workflow dispatch

See `.github/workflows/test.yml` for configuration.

### Test Execution in CI

1. **Unit Tests** - Fast, run first
2. **Integration Tests** - Require backend
3. **E2E Tests** - Full stack, may be slower
4. **Linting** - C# format check and ESLint/Prettier

### Artifacts

Test results and coverage reports are uploaded as artifacts:
- Test results: `**/TestResults/**/*.trx`
- Coverage: `**/TestResults/**/coverage.cobertura.xml`

### Retry Policy

Currently, CI does not automatically retry failed tests. If a test is flaky:
1. Mark it with `[Trait("Flaky", "true")]`
2. Fix the root cause
3. See [FLAKY_TESTS.md](./FLAKY_TESTS.md) for mitigation strategies

## Related Documentation

- **[FLAKY_TESTS.md](./FLAKY_TESTS.md)** - Flaky test mitigation strategies
- **[testing-master-plan.md](./testing-master-plan.md)** - Master plan for test implementation
- **[contracts-v1.md](./contracts-v1.md)** - Contract documentation
- **[simkit.md](./simkit.md)** - SimKit simulation framework documentation

## Quick Reference

### Run All Tests
```bash
dotnet test
```

### Run with Coverage
```bash
dotnet test --collect:"XPlat Code Coverage" --settings:.runsettings
```

### Run E2E Only
```bash
dotnet test --filter "Category=E2E"
```

### Run Smoke Test
```bash
# PowerShell
pwsh tools/smoke-test.ps1

# Bash
./tools/smoke-test.sh
```

### Check Formatting
```bash
dotnet format --verify-no-changes
```

### Run Linting
```bash
# C# formatting
dotnet format --verify-no-changes

# Frontend (from web directory)
cd web
npx eslint . --ext .js,.ts,.svelte
npx prettier --check .
```

## Getting Help

If you encounter issues:

1. Check this documentation
2. Review [FLAKY_TESTS.md](./FLAKY_TESTS.md) for flaky test guidance
3. Check test logs with `--verbosity detailed`
4. Review CI test results for patterns
5. Create a GitHub issue with:
   - Test name and project
   - Error message
   - Steps to reproduce
   - Environment details

