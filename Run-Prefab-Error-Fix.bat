@echo off
title Doctor Who VR - Fix Prefab Error
cd /d "%~dp0"
powershell.exe -NoProfile -ExecutionPolicy Bypass -File "%~dp0Fix-Portal-Prefab-Error.ps1"
