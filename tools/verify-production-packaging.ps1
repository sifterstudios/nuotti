param(
    [Parameter(Mandatory = $true)]
    [string]$Manifest
)

$resources = (Get-Content -LiteralPath $Manifest -Raw | ConvertFrom-Json).resources
$names = @($resources.PSObject.Properties.Name)
$requiredCloud = @('postgres', 'nuotti', 'realtime', 'storage', 'assets', 'backend', 'audience', 'performer')
$localOnly = @('show-agent', 'projector')
$missing = @($requiredCloud | Where-Object { $_ -notin $names })
$leaked = @($localOnly | Where-Object { $_ -in $names })

if ($missing.Count -gt 0) { throw "Cloud manifest is missing: $($missing -join ', ')" }
if ($leaked.Count -gt 0) { throw "Local resources leaked into cloud manifest: $($leaked -join ', ')" }

Write-Output "Production packaging boundary verified ($($names.Count) resources)."
