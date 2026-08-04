@echo off
title Doctor Who VR - GitHub Auto Sync
cd /d "%~dp0"
powershell.exe -NoProfile -ExecutionPolicy Bypass -File "%~dp0Start-ChatGPTSync.ps1"
echo.
pause
