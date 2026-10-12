[CmdletBinding()]
param(
    [switch]$Pull,
    [string]$GameRoot = "C:\Program Files\Epic Games\GTAVEnhanced"
)

$ErrorActionPreference = "Stop"
$RepositoryRoot = Split-Path -Parent $PSScriptRoot
$BuildScript = Join-Path $RepositoryRoot "build.bat"
$VerifyScript = Join-Path $RepositoryRoot "tools\Verify-DWE-ReloadReady.ps1"
$ScriptsPath = Join-Path $GameRoot "scripts"

Write-Host "============================================================" -ForegroundColor Cyan
Write-Host " Dynamic World Engine - Pull / Build / Deploy / Verify" -ForegroundColor Cyan
Write-Host "============================================================" -ForegroundColor Cyan
Write-Host "Repository : $RepositoryRoot"
Write-Host "GTA scripts: $ScriptsPath"
Write-Host ""

if (-not (Test-Path -LiteralPath $BuildScript -PathType Leaf)) {
    throw "build.bat not found at '$BuildScript'."
}

if ($Pull) {
    Write-Host "[DWE] Pulling latest main..." -ForegroundColor Yellow
    Push-Location $RepositoryRoot
    try {
        & git pull --ff-only
        if ($LASTEXITCODE -ne 0) {
            throw "git pull failed with exit code $LASTEXITCODE."
        }
    }
    finally {
        Pop-Location
    }
}

Write-Host "[DWE] Building from repository source..." -ForegroundColor Yellow
Push-Location $RepositoryRoot
try {
    & cmd.exe /c $BuildScript
    if ($LASTEXITCODE -ne 0) {
        throw "build.bat failed with exit code $LASTEXITCODE."
    }
}
finally {
    Pop-Location
}

Write-Host ""
Write-Host "[DWE] Verifying compiled/deployed assembly..." -ForegroundColor Yellow
& powershell.exe -NoProfile -ExecutionPolicy Bypass -File $VerifyScript -RepositoryRoot $RepositoryRoot -GameScriptsPath $ScriptsPath
if ($LASTEXITCODE -ne 0) {
    throw "Reload-readiness verification failed."
}

Write-Host ""
Write-Host "[DWE] DONE." -ForegroundColor Green
Write-Host "Keep GTA V Enhanced running and press Insert to reload ScriptHookVDotNet."
Write-Host "Do not restart the game unless the runtime itself requires it."
