[CmdletBinding()]
param(
    [string]$RepositoryRoot = (Split-Path -Parent $PSScriptRoot),
    [string]$GameScriptsPath = "C:\Program Files\Epic Games\GTAVEnhanced\scripts",
    [string]$AssemblyPath = ""
)

$ErrorActionPreference = "Stop"

function Fail([string]$Message) {
    Write-Host "[DWE][FAIL] $Message" -ForegroundColor Red
    exit 1
}

function Pass([string]$Message) {
    Write-Host "[DWE][OK]   $Message" -ForegroundColor Green
}

if ([string]::IsNullOrWhiteSpace($AssemblyPath)) {
    $AssemblyPath = Join-Path $RepositoryRoot "dist\DynamicWorldEngine.dll"
}

$shvdnReference = Join-Path $RepositoryRoot "lib\ScriptHookVDotNet3.dll"
$legacyReference = Join-Path $RepositoryRoot "lib\ScriptHookVDotNet.dll"

if (-not (Test-Path -LiteralPath $AssemblyPath -PathType Leaf)) {
    Fail "Built assembly not found: $AssemblyPath. Run build.bat first."
}

if (-not (Test-Path -LiteralPath $shvdnReference -PathType Leaf)) {
    Fail "Required ScriptHookVDotNet3 reference is missing: $shvdnReference"
}

if (Test-Path -LiteralPath $legacyReference -PathType Leaf) {
    Write-Host "[DWE][WARN] Legacy ScriptHookVDotNet.dll exists in lib/. It must not be used for the DWE build." -ForegroundColor Yellow
}

try {
    # Load assemblies from their explicit paths. GetReferencedAssemblies() reads the compiled
    # reference table and does not require loading referenced dependencies. Unlike
    # ReflectionOnlyLoadFrom(), Assembly.LoadFile() is supported by modern PowerShell/.NET.
    $null = [Reflection.Assembly]::LoadFile((Resolve-Path -LiteralPath $shvdnReference).Path)

    $newtonsoftReference = Join-Path $RepositoryRoot "lib\Newtonsoft.Json.dll"
    if (Test-Path -LiteralPath $newtonsoftReference -PathType Leaf) {
        $null = [Reflection.Assembly]::LoadFile((Resolve-Path -LiteralPath $newtonsoftReference).Path)
    }

    $assembly = [Reflection.Assembly]::LoadFile((Resolve-Path -LiteralPath $AssemblyPath).Path)
    $references = @($assembly.GetReferencedAssemblies())
}
catch {
    Fail "Could not inspect assembly metadata: $($_.Exception.Message)"
}

$referenceNames = @($references | ForEach-Object { $_.Name })

if ($referenceNames -notcontains "ScriptHookVDotNet3") {
    Fail "DynamicWorldEngine.dll does not reference ScriptHookVDotNet3. The compiled DLL is not reload-safe for the installed SHVDN3 runtime."
}
Pass "Compiled DLL references ScriptHookVDotNet3."

if ($referenceNames -contains "ScriptHookVDotNet") {
    Fail "DynamicWorldEngine.dll still references legacy ScriptHookVDotNet. Rebuild from current source/reference set."
}
Pass "Compiled DLL has no legacy ScriptHookVDotNet dependency."

$scriptType = $assembly.GetType("DynamicWorldEngine.DynamicWorldEngineScript", $false, $false)
if ($null -eq $scriptType) {
    Fail "DynamicWorldEngine.DynamicWorldEngineScript was not found in the compiled assembly."
}
Pass "DynamicWorldEngineScript entry type is present."

$referenceAssemblyName = [Reflection.AssemblyName]::GetAssemblyName((Resolve-Path -LiteralPath $shvdnReference).Path)
Write-Host "[DWE] SHVDN3 reference: $($referenceAssemblyName.FullName)"
Write-Host "[DWE] Built assembly:     $((Resolve-Path -LiteralPath $AssemblyPath).Path)"

if (-not (Test-Path -LiteralPath $GameScriptsPath -PathType Container)) {
    Write-Host "[DWE][WARN] GTA scripts directory does not exist: $GameScriptsPath" -ForegroundColor Yellow
    Write-Host "[DWE] Build validation passed; deployment validation skipped."
    exit 0
}

$deployedAssembly = Join-Path $GameScriptsPath "DynamicWorldEngine.dll"
if (-not (Test-Path -LiteralPath $deployedAssembly -PathType Leaf)) {
    Write-Host "[DWE][WARN] No deployed DynamicWorldEngine.dll found in GTA scripts directory yet." -ForegroundColor Yellow
    Write-Host "[DWE] Build validation passed; deployment validation skipped."
    exit 0
}

$builtHash = (Get-FileHash -LiteralPath $AssemblyPath -Algorithm SHA256).Hash
$deployedHash = (Get-FileHash -LiteralPath $deployedAssembly -Algorithm SHA256).Hash
if ($builtHash -ne $deployedHash) {
    Fail "Deployed DynamicWorldEngine.dll does not match the freshly built DLL. GTA would still load an older build."
}
Pass "GTA scripts DLL matches the freshly built DLL."

# The SHA-256 comparison above proves these are byte-for-byte identical assemblies.
# Do not ReflectionOnlyLoadFrom() the deployed path: .NET Framework reflection-only
# loading allows an assembly identity to be loaded from only one location per AppDomain.
# Reuse the already inspected reference list for the identical deployed file.
$deployedReferences = $referenceNames

if ($deployedReferences -notcontains "ScriptHookVDotNet3") {
    Fail "Deployed DynamicWorldEngine.dll does not reference ScriptHookVDotNet3."
}
if ($deployedReferences -contains "ScriptHookVDotNet") {
    Fail "Deployed DynamicWorldEngine.dll references legacy ScriptHookVDotNet."
}
Pass "Deployed DLL is SHVDN3-compatible and ready for Insert reload."

Write-Host ""
Write-Host "[DWE] Reload contract satisfied:" -ForegroundColor Cyan
Write-Host "      1. Build from repository source"
Write-Host "      2. Deploy matching DynamicWorldEngine.dll to GTA scripts"
Write-Host "      3. Keep GTA running"
Write-Host "      4. Press Insert to reload ScriptHookVDotNet"
Write-Host "      5. Verify the DWE startup log"
