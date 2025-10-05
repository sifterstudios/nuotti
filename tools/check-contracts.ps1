<#
A10 — C# ⇄ TS drift check (best‑effort)

Usage (from repo root):
  pwsh -File tools/check-contracts.ps1

What it does:
  1) Runs `dotnet msbuild` with target GenerateDocumentationFile for Nuotti.Contracts
  2) Parses the generated XML doc to collect public C# type names under Nuotti.Contracts.V1.*
  3) Parses web/shared/contracts.ts to collect exported TS type/interface names
  4) Prints a simple diff and exits with non‑zero code if drift is detected

Notes:
  - This is intentionally simple; it checks only top-level type name presence.
  - It ignores generic arity (e.g., `Type` vs `Type`1) and nested type `+` qualifiers.
#>
param(
  [string]$ContractsCsproj = "Nuotti.Contracts\Nuotti.Contracts.csproj",
  [string]$TsContractsFile = "Nuotti.Contracts\web\shared\contracts.ts",
  [string]$CsNamespacePrefix = "Nuotti.Contracts.V1."
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

function Write-Info($msg) { Write-Host "[check-contracts] $msg" }
function Write-Warn($msg) { Write-Host "[check-contracts] WARN: $msg" -ForegroundColor Yellow }
function Write-Err($msg)  { Write-Host "[check-contracts] ERROR: $msg" -ForegroundColor Red }

if (-not (Test-Path $ContractsCsproj)) {
  Write-Err "C# project not found: $ContractsCsproj"
  exit 2
}
if (-not (Test-Path $TsContractsFile)) {
  Write-Err "TS contracts file not found: $TsContractsFile"
  exit 2
}

# 1) Generate XML documentation via msbuild target
$projDir = Split-Path -Parent $ContractsCsproj
$projDirAbs = (Resolve-Path -LiteralPath $projDir).ProviderPath
$docOutAbs = Join-Path $projDirAbs 'obj\contracts-doc.xml'
Write-Info "Building C# project with documentation enabled …"
& dotnet build $ContractsCsproj -c Debug -nologo /p:GenerateDocumentationFile=true /p:DocumentationFile=$docOutAbs | Out-Null

# 2) Verify the produced XML file
if (-not (Test-Path $docOutAbs)) {
  Write-Err "XML documentation file was not produced: $docOutAbs"
  Write-Warn "Ensure the project builds and the target succeeded."
  exit 2
}

Write-Info "Using XML doc: $docOutAbs"

# 3) Parse C# type names from XML doc
[xml]$xml = Get-Content -LiteralPath $docOutAbs
$members = @($xml.doc.members.member)

# Some XMLs might not include members when no XML-doc comments exist; we still can get type nodes via member names
$csTypeNames = New-Object System.Collections.Generic.HashSet[string]
foreach ($m in $members) {
  $nameAttr = $m.name
  if (-not $nameAttr) { continue }
  if ($nameAttr -notlike 'T:*') { continue }
  $full = $nameAttr.Substring(2) # strip 'T:'
  if (-not $full.StartsWith($CsNamespacePrefix)) { continue }
  # Strip generic arity suffix (e.g., Foo`1)
  $full = $full -replace '`\d+', ''
  # Replace nested type separator '+' with '.' for consistency then take simple name
  $simple = ($full -replace '\+', '.').Split('.')[-1]
  [void]$csTypeNames.Add($simple)
}

if ($csTypeNames.Count -eq 0) {
  Write-Warn "No C# types discovered under namespace prefix '$CsNamespacePrefix'."
}

# 4) Parse TS exported names (types and interfaces)
$tsLines = Get-Content -LiteralPath $TsContractsFile
$tsNames = New-Object System.Collections.Generic.HashSet[string]
$tsRegex = '^[\s]*export\s+(type|interface)\s+([A-Za-z0-9_]+)'
foreach ($line in $tsLines) {
  $m = [System.Text.RegularExpressions.Regex]::Match($line, $tsRegex)
  if ($m.Success) {
    $name = $m.Groups[2].Value
    [void]$tsNames.Add($name)
  }
}

if ($tsNames.Count -eq 0) {
  Write-Warn "No exported TS types/interfaces found in $TsContractsFile"
}

# 5) Compare sets (simple list-based)
$csOnly = @($csTypeNames | Where-Object { -not $tsNames.Contains($_) })
$tsOnly = @($tsNames | Where-Object { -not $csTypeNames.Contains($_) })

$hasDrift = ($csOnly.Count -gt 0) -or ($tsOnly.Count -gt 0)

if (-not $hasDrift) {
  Write-Host "\n✅ Contracts drift check: OK (names match)" -ForegroundColor Green
  exit 0
}

Write-Host "\n❌ Contracts drift detected:" -ForegroundColor Red
if ($csOnly.Count -gt 0) {
  Write-Host "  Present in C# only (missing in TS):" -ForegroundColor Yellow
  $csOnlySorted = $csOnly | Sort-Object
  foreach ($n in $csOnlySorted) { Write-Host "   - $n" }
}
if ($tsOnly.Count -gt 0) {
  Write-Host "  Present in TS only (missing in C#):" -ForegroundColor Yellow
  $tsOnlySorted = $tsOnly | Sort-Object
  foreach ($n in $tsOnlySorted) { Write-Host "   - $n" }
}

Write-Host "\nHint: Update either the C# contracts (Nuotti.Contracts) or TS mirrors (web/shared/contracts.ts)." -ForegroundColor DarkGray
exit 1
