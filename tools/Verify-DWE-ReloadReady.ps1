param(
    [string]$GtaRoot = 'C:\Program Files\Epic Games\GTAVEnhanced'
)

$ErrorActionPreference = 'Stop'
$Scripts = Join-Path $GtaRoot 'scripts'

Write-Host "DWE reload-readiness check"
Write-Host "GTA root : $GtaRoot"
Write-Host "Scripts  : $Scripts"

if (-not (Test-Path -LiteralPath $GtaRoot)) {
    throw "GTA V Enhanced was not found at '$GtaRoot'. Pass -GtaRoot with the actual installation path."
}

if (-not (Test-Path -LiteralPath $Scripts)) {
    throw "The ScriptHookVDotNet scripts directory does not exist: '$Scripts'"
}

$dwe = Join-Path $Scripts 'DynamicWorldEngine.dll'
$shvdn3 = Join-Path $Scripts 'ScriptHookVDotNet3.dll'
$shvdnLegacy = Join-Path $Scripts 'ScriptHookVDotNet.dll'

if (-not (Test-Path -LiteralPath $dwe)) {
    throw "DynamicWorldEngine.dll is missing from '$Scripts'. Pull/install the DWE runtime into the GTA scripts directory first."
}

if (-not (Test-Path -LiteralPath $shvdn3)) {
    throw "ScriptHookVDotNet3.dll is missing from '$Scripts'."
}

Write-Host "Found DynamicWorldEngine.dll"
Write-Host "Found ScriptHookVDotNet3.dll"

if (-not (Test-Path -LiteralPath $shvdnLegacy)) {
    Write-Warning "ScriptHookVDotNet.dll is not present. The current DWE binary in this repository has previously reported an assembly dependency on the legacy ScriptHookVDotNet identity. Do not treat Insert-reload as validated until the DWE binary is rebuilt against the installed ScriptHookVDotNet3 API."
}

Write-Host ''
Write-Host 'Deployment rule:'
Write-Host '  1. The repository runtime files must be present in the GTA scripts directory.'
Write-Host '  2. Start GTA V Enhanced.'
Write-Host '  3. After a git pull that changes the DWE runtime, press Insert to reload ScriptHookVDotNet scripts.'
Write-Host '  4. If the readiness check reports a dependency warning, the binary must be rebuilt before gameplay validation.'
