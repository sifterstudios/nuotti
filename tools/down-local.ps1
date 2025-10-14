param(
  [switch]$Prune
)

# Stops and removes local containers created with the dev compose files.
# Usage:
#   ./tools/down-local.ps1
#   ./tools/down-local.ps1 -Prune   # also prune dangling images

$ErrorActionPreference = 'Stop'

$repoRoot = Split-Path -Parent $PSScriptRoot
$composeBase = Join-Path $repoRoot 'deploy/docker-compose.yml'
$composeOverride = Join-Path $repoRoot 'deploy/docker-compose.override.yml'

Write-Host "Stopping containers..." -ForegroundColor Cyan
& docker compose -f $composeBase -f $composeOverride down --remove-orphans
if ($LASTEXITCODE -ne 0) { throw "docker compose down failed with exit code $LASTEXITCODE" }

if ($Prune) {
  Write-Host "Pruning dangling images..." -ForegroundColor Cyan
  & docker image prune -f
}
