@echo off
echo Starting Scrappy Bot and Web Server silently...
REM Stop the Windows service if it's running, to prevent port conflicts
net stop ScrappyBot > nul 2>&1

REM Start Scrappy.exe from the publish directory using PowerShell to hide the window
powershell -WindowStyle Hidden -Command "Start-Process '.\publish\Scrappy.exe'"
echo Scrappy Bot and Web Server started.
