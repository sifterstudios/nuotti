param(
  [switch]$Up
)

# Builds Docker images locally using the standalone local compose file.
# If -Up is specified, it will also start the stack in detached mode.

$ErrorActionPreference = 'Stop'

$repoRoot = Split-Path -Parent $PSScriptRoot
$composeLocal = Join-Path $repoRoot 'deploy/docker-compose.local.yml'

Write-Host "Using local compose file:" -ForegroundColor Cyan
Write-Host "  $composeLocal" -ForegroundColor DarkCyan

# Show Docker versions for context
Write-Host "Docker versions:" -ForegroundColor Cyan
& docker version
& docker compose version

# Build images
Write-Host "Building images..." -ForegroundColor Cyan
& docker compose -f $composeLocal build --pull
if ($LASTEXITCODE -ne 0) { throw "docker compose build failed with exit code $LASTEXITCODE" }

if ($Up) {
  Write-Host "Starting containers (detached)..." -ForegroundColor Cyan
  & docker compose -f $composeLocal up -d --remove-orphans
  if ($LASTEXITCODE -ne 0) { throw "docker compose up failed with exit code $LASTEXITCODE" }
  Write-Host "Containers are up. Exposed ports:" -ForegroundColor Green
  Write-Host "  API:      http://localhost:5210" -ForegroundColor Green
  Write-Host "  Audience: http://localhost:5280" -ForegroundColor Green
  Write-Host "  Web:      http://localhost:5380" -ForegroundColor Green
}
