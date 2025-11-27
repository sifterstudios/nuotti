# Nuotti Projector E2E Test Runner
# F20 - Playwright E2E visual regression testing

param(
    [string]$TestFilter = "",
    [switch]$Headless = $true,
    [switch]$InstallBrowsers = $false,
    [switch]$Visual = $false,
    [switch]$Performance = $false,
    [switch]$Interaction = $false
)

Write-Host "🎭 Nuotti Projector E2E Test Runner" -ForegroundColor Cyan
Write-Host "=====================================" -ForegroundColor Cyan

# Install Playwright browsers if requested
if ($InstallBrowsers) {
    Write-Host "📦 Installing Playwright browsers..." -ForegroundColor Yellow
    dotnet run --project . -- playwright install
    if ($LASTEXITCODE -ne 0) {
        Write-Host "❌ Failed to install Playwright browsers" -ForegroundColor Red
        exit 1
    }
    Write-Host "✅ Playwright browsers installed" -ForegroundColor Green
}

# Build the test project
Write-Host "🔨 Building test project..." -ForegroundColor Yellow
dotnet build
if ($LASTEXITCODE -ne 0) {
    Write-Host "❌ Build failed" -ForegroundColor Red
    exit 1
}
Write-Host "✅ Build successful" -ForegroundColor Green

# Determine test filter
$filter = ""
if ($Visual) {
    $filter = "Category=Visual"
} elseif ($Performance) {
    $filter = "Category=Performance"
} elseif ($Interaction) {
    $filter = "Category=Interaction"
} elseif ($TestFilter) {
    $filter = $TestFilter
}

# Build test command
$testCmd = "dotnet test"
if ($filter) {
    $testCmd += " --filter `"$filter`""
}
$testCmd += " --logger trx --results-directory TestResults"

# Set environment variables for test configuration
if (-not $Headless) {
    $env:NUOTTI_TEST_HEADLESS = "false"
}

Write-Host "🧪 Running tests..." -ForegroundColor Yellow
if ($filter) {
    Write-Host "   Filter: $filter" -ForegroundColor Gray
}
Write-Host "   Headless: $Headless" -ForegroundColor Gray

# Run tests
Invoke-Expression $testCmd
$testResult = $LASTEXITCODE

# Report results
if ($testResult -eq 0) {
    Write-Host "✅ All tests passed!" -ForegroundColor Green
} else {
    Write-Host "❌ Some tests failed" -ForegroundColor Red
}

# Show screenshot location
$screenshotPath = Join-Path $PWD "Screenshots"
if (Test-Path $screenshotPath) {
    $screenshotCount = (Get-ChildItem $screenshotPath -Filter "*.png" | Measure-Object).Count
    if ($screenshotCount -gt 0) {
        Write-Host "📸 $screenshotCount screenshots saved to: $screenshotPath" -ForegroundColor Blue
    }
}

exit $testResult
