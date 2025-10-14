param(
  [switch]$Prune
)

# Stops and removes local containers created with the local compose file.
# Usage:
#   ./tools/down-local.ps1
#   ./tools/down-local.ps1 -Prune   # also prune dangling images

$ErrorActionPreference = 'Stop'

$repoRoot = Split-Path -Parent $PSScriptRoot
$composeLocal = Join-Path $repoRoot 'deploy/docker-compose.local.yml'

Write-Host "Stopping containers..." -ForegroundColor Cyan
& docker compose -f $composeLocal down --remove-orphans
if ($LASTEXITCODE -ne 0) { throw "docker compose down failed with exit code $LASTEXITCODE" }

if ($Prune) {
  Write-Host "Pruning dangling images..." -ForegroundColor Cyan
  & docker image prune -f
}
