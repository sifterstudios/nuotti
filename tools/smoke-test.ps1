# Smoke test script for local development
# This script performs a quick sanity check that the backend is working
# Usage: pwsh -File tools/smoke-test.ps1 [--backend-url <url>] [--session <code>]

param(
    [string]$BackendUrl = "http://localhost:5240",
    [string]$Session = "smoke-test"
)

$ErrorActionPreference = 'Stop'

function Write-Info($msg) { Write-Host "[smoke-test] $msg" -ForegroundColor Cyan }
function Write-Success($msg) { Write-Host "[smoke-test] $msg" -ForegroundColor Green }
function Write-Err($msg) { Write-Host "[smoke-test] ERROR: $msg" -ForegroundColor Red }

Write-Info "Starting smoke test..."
Write-Info "Backend URL: $BackendUrl"
Write-Info "Session: $Session"

# Check if backend is already running
Write-Info "Checking if backend is running..."
try {
    $healthCheck = Invoke-WebRequest -Uri "$BackendUrl/health/live" -Method Get -TimeoutSec 2 -ErrorAction Stop
    if ($healthCheck.StatusCode -eq 200) {
        Write-Info "Backend is already running"
    } else {
        Write-Err "Backend health check returned status $($healthCheck.StatusCode)"
        exit 1
    }
} catch {
    Write-Err "Backend is not running or not accessible at $BackendUrl"
    Write-Err "Please start the backend first: dotnet run --project Nuotti.Backend"
    exit 1
}

# Create session
Write-Info "Creating session..."
try {
    $createResp = Invoke-WebRequest -Uri "$BackendUrl/api/sessions/$Session" -Method Post -ErrorAction Stop
    if ($createResp.StatusCode -eq 200) {
        Write-Info "Session created successfully"
    } else {
        Write-Err "Failed to create session: $($createResp.StatusCode)"
        exit 1
    }
} catch {
    Write-Err "Failed to create session: $_"
    exit 1
}

# Upload manifest with one song
Write-Info "Uploading manifest..."
$manifest = @{
    songs = @(
        @{
            title = "Smoke Test Song"
            artist = "Test Artist"
            file = "https://example.com/smoke-test.mp3"
        }
    )
} | ConvertTo-Json

try {
    $manifestResp = Invoke-WebRequest -Uri "$BackendUrl/api/manifest/$Session" -Method Post -Body $manifest -ContentType "application/json" -ErrorAction Stop
    if ($manifestResp.StatusCode -eq 202) {
        Write-Info "Manifest uploaded successfully"
    } else {
        Write-Err "Failed to upload manifest: $($manifestResp.StatusCode)"
        exit 1
    }
} catch {
    Write-Err "Failed to upload manifest: $_"
    exit 1
}

# Push a question
Write-Info "Pushing question..."
$question = @{
    text = "What is the answer?"
    options = @("Option A", "Option B", "Option C")
    sessionCode = $Session
    issuedByRole = 2  # Performer
    issuedById = "smoke-test"
} | ConvertTo-Json

try {
    $questionResp = Invoke-WebRequest -Uri "$BackendUrl/api/pushQuestion/$Session" -Method Post -Body $question -ContentType "application/json" -ErrorAction Stop
    if ($questionResp.StatusCode -eq 202) {
        Write-Info "Question pushed successfully"
    } else {
        Write-Err "Failed to push question: $($questionResp.StatusCode)"
        exit 1
    }
} catch {
    Write-Err "Failed to push question: $_"
    exit 1
}

# Verify session status
Write-Info "Verifying session status..."
try {
    $statusResp = Invoke-WebRequest -Uri "$BackendUrl/status/$Session" -Method Get -ErrorAction Stop
    if ($statusResp.StatusCode -eq 200) {
        $status = $statusResp.Content | ConvertFrom-Json
        Write-Info "Session status verified: Phase = $($status.phase)"
    } else {
        Write-Err "Failed to get session status: $($statusResp.StatusCode)"
        exit 1
    }
} catch {
    Write-Err "Failed to get session status: $_"
    exit 1
}

Write-Success "SMOKE PASS"
exit 0

