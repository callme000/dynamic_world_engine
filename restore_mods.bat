@echo off
echo ===================================================
echo   RESTORE MODS TO GTA V (SINGLE-PLAYER MODE)
echo ===================================================

if exist "%~dp0restore_mods.ps1" (
    powershell -ExecutionPolicy Bypass -File "%~dp0restore_mods.ps1"
) else if exist "C:\Users\melod\Documents\gtavmods\restore_mods.ps1" (
    powershell -ExecutionPolicy Bypass -File "C:\Users\melod\Documents\gtavmods\restore_mods.ps1"
) else if exist "C:\Users\melod\Desktop\mod backup\restore_mods.ps1" (
    powershell -ExecutionPolicy Bypass -File "C:\Users\melod\Desktop\mod backup\restore_mods.ps1"
) else (
    echo ERROR: restore_mods.ps1 not found!
)
pause
