@echo off
setlocal
title Doctor Who VR - Fix Prefab Error v2
cd /d "%~dp0"

echo.
echo Doctor Who VR - Prefab Error Fix v2
echo Project: %CD%
echo.

set "TARGET=%CD%\Assets\DoctorWhoVR\Editor\PortalProjectBootstrap.cs"

if not exist "%TARGET%" (
    echo ERROR: Could not find:
    echo %TARGET%
    echo.
    echo Put this BAT directly beside Assets, Packages, ProjectSettings, and .git.
    echo.
    pause
    exit /b 1
)

powershell.exe -NoProfile -ExecutionPolicy Bypass -Command ^
  "$p = $env:TARGET;" ^
  "$old = 'UnityEngine.Object.DestroyImmediate(child.gameObject);';" ^
  "$new = 'child.gameObject.SetActive(false);';" ^
  "$c = [System.IO.File]::ReadAllText($p);" ^
  "if ($c.Contains($new)) { Write-Host 'The fix is already installed.' -ForegroundColor Green; exit 0 };" ^
  "if (-not $c.Contains($old)) { Write-Host 'ERROR: Expected old line was not found.' -ForegroundColor Red; exit 2 };" ^
  "[System.IO.File]::Copy($p, ($p + '.backup'), $true);" ^
  "$c = $c.Replace($old, $new);" ^
  "[System.IO.File]::WriteAllText($p, $c, [System.Text.UTF8Encoding]::new($false));" ^
  "Write-Host 'Fixed PortalProjectBootstrap.cs successfully.' -ForegroundColor Green;"

set "RESULT=%ERRORLEVEL%"
echo.

if not "%RESULT%"=="0" (
    echo The fix failed with error code %RESULT%.
    echo Send a screenshot of this window.
    echo.
    pause
    exit /b %RESULT%
)

echo Fix completed.
echo Return to Unity and wait for it to compile.
echo Then click:
echo Doctor Who VR ^> Rebuild Clean Portal Prototype
echo.
echo Keep the Auto Sync window open so the fix uploads.
echo.
pause
