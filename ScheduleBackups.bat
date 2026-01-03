@echo off
set SCRIPT_PATH=C:\DiscordBot\Scrappy\Backup-Scrappy.ps1

:: Create Daily Backup Task (Every day at 6:00 AM)
schtasks /create /tn "Scrappy_Daily_Backup" /tr "powershell.exe -ExecutionPolicy Bypass -File %SCRIPT_PATH% -Type Daily" /sc daily /st 06:00 /f

:: Create Weekly Backup Task (Every Sunday at 6:00 AM)
schtasks /create /tn "Scrappy_Weekly_Backup" /tr "powershell.exe -ExecutionPolicy Bypass -File %SCRIPT_PATH% -Type Weekly" /sc weekly /d SUN /st 06:00 /f

echo Tasks scheduled successfully.
pause
