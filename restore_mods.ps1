$gtaDir = "C:\Program Files\Epic Games\GTAVEnhanced"
$backupDir = "C:\Users\melod\Desktop\mod backup"

Write-Output "================================================="
Write-Output "  RESTORE MODS TO GTA V (SINGLE-PLAYER MODE)"
Write-Output "================================================="

# Check if GTA V or launchers are currently running
$runningProc = Get-Process -Name "GTA5_Enhanced", "GTA5_Enhanced_BE", "PlayGTAV" -ErrorAction SilentlyContinue
if ($runningProc) {
    Write-Output "ERROR: GTA V or its launcher is currently running."
    Write-Output "Please close the game completely before restoring mod files."
    exit 1
}

if (-not (Test-Path $backupDir)) {
    Write-Output "ERROR: Backup directory not found at $backupDir"
    exit 1
}

$modFolders = @(
    "scripts",
    "DynamicWorldEngineData",
    "LLAIBridge",
    "mods"
)

$modFiles = @(
    "dinput8.dll",
    "ScriptHookV.dll",
    "ScriptHookV.log",
    "ScriptHookVDotNet.asi",
    "ScriptHookVDotNet.ini",
    "ScriptHookVDotNet.log",
    "ScriptHookVDotNet2.dll",
    "ScriptHookVDotNet3.dll",
    "NativeTrainer.asi",
    "MinHook.x64.dll",
    "asiloader.log",
    "iFruitAddon2.log",
    "args.txt",
    "xinput1_4.dll"
)

# Also detect any additional .asi mods backed up in $backupDir
$extraAsiFiles = Get-ChildItem -Path $backupDir -Filter "*.asi" -ErrorAction SilentlyContinue | Select-Object -ExpandProperty Name
foreach ($extra in $extraAsiFiles) {
    if ($modFiles -notcontains $extra) {
        $modFiles += $extra
    }
}

Write-Output "`n1. Restoring mod folders to GTA V..."
foreach ($folder in $modFolders) {
    $srcPath = Join-Path $backupDir $folder
    $destPath = Join-Path $gtaDir $folder
    if (Test-Path $srcPath) {
        Write-Output "  -> Restoring folder: $folder"
        Copy-Item -Path $srcPath -Destination $destPath -Recurse -Force
    }
}

Write-Output "`n2. Restoring mod files to GTA V..."
foreach ($file in $modFiles) {
    $srcPath = Join-Path $backupDir $file
    $destPath = Join-Path $gtaDir $file
    if (Test-Path $srcPath) {
        Write-Output "  -> Restoring file: $file"
        Copy-Item -Path $srcPath -Destination $destPath -Force
    }
}

Write-Output "`n3. Verification:"
Write-Output "  All mod files and scripts restored to $gtaDir"
Write-Output "  Ready for modded Single-Player!"
