$gtaDir = "C:\Program Files\Epic Games\GTAVEnhanced"
$backupDir = "C:\Users\melod\Desktop\mod backup"

Write-Output "================================================="
Write-Output "  BACKING UP MODS FOR GTA ONLINE (CLEAN GAME DIR)"
Write-Output "================================================="

if (-not (Test-Path $backupDir)) {
    New-Item -ItemType Directory -Path $backupDir -Force | Out-Null
}

$modFolders = @(
    "scripts",
    "DynamicWorldEngineData",
    "LLAIBridge"
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

Write-Output "`n1. Copying mod directories to backup..."
foreach ($folder in $modFolders) {
    $srcPath = Join-Path $gtaDir $folder
    $destPath = Join-Path $backupDir $folder
    if (Test-Path $srcPath) {
        Write-Output "  -> Backing up folder: $folder"
        Copy-Item -Path $srcPath -Destination $destPath -Recurse -Force
    }
}

Write-Output "`n2. Copying mod files to backup..."
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
        Write-Output "  WARNING: $check still present!"
        $foundMod = $true
    }
}

if (-not $foundMod) {
    Write-Output "  SUCCESS: GTA V directory is 100% clean and ready for GTA Online & BattlEye!"
} else {
    Write-Output "  ATTENTION: Some mod files could not be removed."
}
