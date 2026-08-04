@echo off
title Doctor Who VR - Install Portal V2
cd /d "%~dp0"
powershell.exe -NoProfile -ExecutionPolicy Bypass -File "%~dp0Install-Portal-V2.ps1"
echo.
pause
