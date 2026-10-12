$gtaDir = "C:\Program Files\Epic Games\GTAVEnhanced"
$backupDir = "C:\Users\melod\Desktop\mod backup"

Write-Output "================================================="
Write-Output "  RESTORE GTA ONLINE (CLEAN GAME FOR BATTLEYE)"
Write-Output "================================================="

# Check if GTA V or launchers are currently running
$runningProc = Get-Process -Name "GTA5_Enhanced", "GTA5_Enhanced_BE", "PlayGTAV" -ErrorAction SilentlyContinue
if ($runningProc) {
    Write-Output "ERROR: GTA V or its launcher is currently running."
    Write-Output "Please close the game completely before restoring GTA Online mode."
    exit 1
}

if (-not (Test-Path $gtaDir)) {
    Write-Output "ERROR: GTA V directory not found at $gtaDir"
    exit 1
}

if (-not (Test-Path $backupDir)) {
    Write-Output "Creating backup directory at $backupDir..."
    New-Item -ItemType Directory -Path $backupDir -Force | Out-Null
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

# Also detect any additional .asi mods in the GTA V root
$extraAsiFiles = Get-ChildItem -Path $gtaDir -Filter "*.asi" -ErrorAction SilentlyContinue | Select-Object -ExpandProperty Name
foreach ($extra in $extraAsiFiles) {
    if ($modFiles -notcontains $extra) {
        $modFiles += $extra
    }
}

Write-Output "`n1. Backing up mod directories to $backupDir..."
foreach ($folder in $modFolders) {
    $srcPath = Join-Path $gtaDir $folder
    $destPath = Join-Path $backupDir $folder
    if (Test-Path $srcPath) {
        Write-Output "  -> Backing up folder: $folder"
        Copy-Item -Path $srcPath -Destination $destPath -Recurse -Force
    }
}

Write-Output "`n2. Backing up mod files to $backupDir..."
foreach ($file in $modFiles) {
    $srcPath = Join-Path $gtaDir $file
    $destPath = Join-Path $backupDir $file
    if (Test-Path $srcPath) {
        Write-Output "  -> Backing up file: $file"
        Copy-Item -Path $srcPath -Destination $destPath -Force
    }
}

Write-Output "`n3. Removing mod folders from GTA V game directory..."
foreach ($folder in $modFolders) {
    $srcPath = Join-Path $gtaDir $folder
    if (Test-Path $srcPath) {
        Write-Output "  -> Removing folder from game: $folder"
        Remove-Item -Path $srcPath -Recurse -Force
    }
}

Write-Output "`n4. Removing mod files from GTA V game directory..."
foreach ($file in $modFiles) {
    $srcPath = Join-Path $gtaDir $file
    if (Test-Path $srcPath) {
        Write-Output "  -> Removing file from game: $file"
        Remove-Item -Path $srcPath -Force
    }
}

Write-Output "`n5. Verifying clean GTA V directory..."
$remainingItems = Get-ChildItem -Path $gtaDir | Select-Object -ExpandProperty Name
$foundMod = $false
foreach ($check in ($modFolders + $modFiles)) {
    if ($remainingItems -contains $check) {
        Write-Output "  WARNING: $check is still present in GTA V folder!"
        $foundMod = $true
    }
}

if (-not $foundMod) {
    Write-Output "`n================================================="
    Write-Output "  SUCCESS: GTA V directory is 100% clean!"
    Write-Output "  Ready for GTA Online & BattlEye!"
    Write-Output "================================================="
} else {
    Write-Output "`n================================================="
    Write-Output "  ATTENTION: Some mod files could not be removed."
    Write-Output "  Please check if a process is locking the files."
    Write-Output "================================================="
}
