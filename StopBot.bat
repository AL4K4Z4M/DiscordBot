@echo off
echo Stopping Scrappy Bot and Web Server...
REM Stop the Windows service if it's running
net stop ScrappyBot > nul 2>&1

REM Find and kill the Scrappy.exe process
taskkill /IM Scrappy.exe /F > nul 2>&1
echo Scrappy Bot and Web Server stopped.
