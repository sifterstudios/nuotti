# Builds and starts the local stack
$ErrorActionPreference = 'Stop'
$repoRoot = Split-Path -Parent $PSScriptRoot
& "$PSScriptRoot/build-local.ps1" -Up
