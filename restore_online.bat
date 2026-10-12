@echo off
echo ===================================================
echo   RESTORE GTA ONLINE (CLEAN GAME FOR BATTLEYE)
echo ===================================================

if exist "%~dp0restore_online.ps1" (
    powershell -ExecutionPolicy Bypass -File "%~dp0restore_online.ps1"
) else if exist "C:\Users\melod\Documents\gtavmods\restore_online.ps1" (
    powershell -ExecutionPolicy Bypass -File "C:\Users\melod\Documents\gtavmods\restore_online.ps1"
) else if exist "C:\Users\melod\Desktop\mod backup\restore_online.ps1" (
    powershell -ExecutionPolicy Bypass -File "C:\Users\melod\Desktop\mod backup\restore_online.ps1"
) else (
    echo ERROR: restore_online.ps1 not found!
)
pause
