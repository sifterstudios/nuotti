# Pre-commit hook script for Nuotti
# Run this before committing to check formatting and linting

Write-Host "Running pre-commit checks..." -ForegroundColor Cyan

$errors = 0

# Check C# formatting
Write-Host "`nChecking C# code formatting..." -ForegroundColor Yellow
$formatResult = dotnet format --verify-no-changes --verbosity quiet
if ($LASTEXITCODE -ne 0) {
    Write-Host "C# formatting errors found. Run 'dotnet format' to fix." -ForegroundColor Red
    $errors++
} else {
    Write-Host "C# formatting OK" -ForegroundColor Green
}

# Check frontend linting (if Node.js is available)
if (Get-Command node -ErrorAction SilentlyContinue) {
    Write-Host "`nChecking frontend code..." -ForegroundColor Yellow
    
    Push-Location web
    try {
        if (Test-Path node_modules) {
            $eslintResult = npx eslint . --ext .js,.ts,.svelte --max-warnings 0 2>&1
            if ($LASTEXITCODE -ne 0) {
                Write-Host "ESLint errors found. Run 'npx eslint . --fix' to fix." -ForegroundColor Red
                $errors++
            } else {
                Write-Host "ESLint OK" -ForegroundColor Green
            }

            $prettierResult = npx prettier --check . 2>&1
            if ($LASTEXITCODE -ne 0) {
                Write-Host "Prettier formatting errors found. Run 'npx prettier --write .' to fix." -ForegroundColor Red
                $errors++
            } else {
                Write-Host "Prettier OK" -ForegroundColor Green
            }
        } else {
            Write-Host "Skipping frontend checks (node_modules not found)" -ForegroundColor Yellow
        }
    } finally {
        Pop-Location
    }
} else {
    Write-Host "`nSkipping frontend checks (Node.js not found)" -ForegroundColor Yellow
}

if ($errors -gt 0) {
    Write-Host "`nPre-commit checks failed. Please fix the errors above." -ForegroundColor Red
    exit 1
} else {
    Write-Host "`nAll pre-commit checks passed!" -ForegroundColor Green
    exit 0
}

