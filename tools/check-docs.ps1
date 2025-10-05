<#!
Simple Markdown link checker for repo docs.

- Scans docs/*.md
- Validates relative links point to existing files
- Validates intra-document anchors (#anchor) exist as headings
- Skips http/https links to avoid network flakiness

Usage (from repo root):
  pwsh -File tools/check-docs.ps1
#>

param(
  [string]$DocsDir = "docs"
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

function Write-Info($msg) { Write-Host "[check-docs] $msg" }
function Write-Warn($msg) { Write-Host "[check-docs] WARN: $msg" -ForegroundColor Yellow }
function Write-Err($msg)  { Write-Host "[check-docs] ERROR: $msg" -ForegroundColor Red }

if (-not (Test-Path -LiteralPath $DocsDir)) {
  Write-Err "Docs directory not found: $DocsDir"
  exit 2
}

$mdFiles = @(Get-ChildItem -LiteralPath $DocsDir -Filter *.md -File -Recurse)
if ($mdFiles.Count -eq 0) {
  Write-Warn "No markdown files found in '$DocsDir'"
  exit 0
}

$broken = @()

function Get-AnchorsFromFile([string]$path) {
  $anchors = New-Object System.Collections.Generic.HashSet[string]
  $lines = Get-Content -LiteralPath $path
  foreach ($l in $lines) {
    # Markdown headings: up to 6 '#', then text
    if ($l -match '^[\s]*#{1,6}\s*(.+)$') {
      $title = $Matches[1].Trim()
      # GitHub-style anchor normalization: lower, replace spaces with '-', remove invalid chars
      $anchor = $title.ToLowerInvariant()
      $anchor = $anchor -replace "[^a-z0-9\-\s]", ''
      $anchor = ($anchor -replace '\s+', '-').Trim('-')
      [void]$anchors.Add($anchor)
    }
  }
  return $anchors
}

$root = (Get-Location).ProviderPath

foreach ($md in $mdFiles) {
  Write-Info "Checking $($md.FullName.Substring($root.Length+1))"
  $content = Get-Content -LiteralPath $md.FullName -Raw
  $anchorsHere = Get-AnchorsFromFile $md.FullName

  $regex = New-Object System.Text.RegularExpressions.Regex "\[[^\]]+\]\(([^)]+)\)", "IgnoreCase"
  $matches = $regex.Matches($content)

  foreach ($m in $matches) {
    $target = $m.Groups[1].Value.Trim()
    if ($target -like 'http://*' -or $target -like 'https://*' -or $target -like 'mailto:*') {
      continue
    }
    if ($target.StartsWith('#')) {
      $frag = $target.Substring(1)
      if (-not $anchorsHere.Contains($frag.ToLowerInvariant())) {
        $broken += "${($md.FullName)}: missing anchor '#$frag'"
      }
      continue
    }

    # Split file and optional anchor
    $filePart = $target
    $fragPart = ''
    $hashIdx = $target.IndexOf('#')
    if ($hashIdx -ge 0) {
      $filePart = $target.Substring(0, $hashIdx)
      $fragPart = $target.Substring($hashIdx + 1)
    }

    $linkedPath = Join-Path -Path $md.DirectoryName -ChildPath $filePart
    $linkedPath = [System.IO.Path]::GetFullPath($linkedPath)
    if (-not (Test-Path -LiteralPath $linkedPath)) {
      $broken += "${($md.FullName)}: broken file link '$target' (resolved '$linkedPath')"
      continue
    }

    if ($fragPart) {
      $anchors = Get-AnchorsFromFile $linkedPath
      if (-not $anchors.Contains($fragPart.ToLowerInvariant())) {
        $broken += "${($md.FullName)}: missing anchor '#$fragPart' in '$filePart'"
      }
    }
  }
}

if ($broken.Count -gt 0) {
  Write-Host "\n❌ Markdown link check failed:" -ForegroundColor Red
  foreach ($b in $broken) { Write-Host " - $b" }
  exit 1
}

Write-Host "\n✅ Markdown link check: OK" -ForegroundColor Green
exit 0
