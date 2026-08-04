@echo off
title Doctor Who VR - Sync Now
cd /d "%~dp0"

git add -A
git diff --cached --quiet
if %errorlevel%==0 (
    echo Nothing new to upload.
    pause
    exit /b 0
)

git commit -m "Manual Unity sync %date% %time%"
if errorlevel 1 (
    echo Commit failed.
    pause
    exit /b 1
)

for /f "delims=" %%B in ('git branch --show-current') do set BRANCH=%%B
git push -u origin "%BRANCH%"
echo.
echo Upload finished.
pause
