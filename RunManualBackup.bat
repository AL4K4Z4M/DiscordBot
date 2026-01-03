@echo off
set SCRIPT_PATH=C:\DiscordBot\Scrappy\Backup-Scrappy.ps1

echo ============================================
echo        Scrappy Manual Backup Tool
echo ============================================
echo.
echo 1. Daily Style (Quick: secrets.json and scrappy.db)
echo 2. Weekly Style (Full: ZIP of entire project)
echo 3. Exit
echo.
set /p choice="Choose backup type (1-3): "

if "%choice%"=="1" (
    echo Running Quick Backup...
    powershell.exe -ExecutionPolicy Bypass -File "%SCRIPT_PATH%" -Type Daily
    echo Done!
) else if "%choice%"=="2" (
    echo Running Full ZIP Backup...
    powershell.exe -ExecutionPolicy Bypass -File "%SCRIPT_PATH%" -Type Weekly
    echo Done!
) else (
    echo Exiting.
)

pause
