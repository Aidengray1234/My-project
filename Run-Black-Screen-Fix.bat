@echo off
setlocal
title Doctor Who VR - Fix Black Screen
cd /d "%~dp0"

echo.
echo Doctor Who VR - Black Screen / Missing Camera Fix
echo Project: %CD%
echo.

set "BOOTSTRAP=%CD%\Assets\DoctorWhoVR\Editor\PortalProjectBootstrap.cs"
set "DISABLER=%CD%\Assets\DoctorWhoVR\Scripts\ComfortVignetteDisabler.cs"

if not exist "%BOOTSTRAP%" (
    echo ERROR: Could not find:
    echo %BOOTSTRAP%
    echo.
    echo Put this BAT directly beside Assets, Packages, ProjectSettings, and .git.
    pause
    exit /b 1
)

if not exist "%DISABLER%" (
    echo ERROR: Could not find:
    echo %DISABLER%
    pause
    exit /b 1
)

powershell.exe -NoProfile -ExecutionPolicy Bypass -Command ^
  "$bootstrap = $env:BOOTSTRAP;" ^
  "$disabler = $env:DISABLER;" ^
  "$utf8 = [System.Text.UTF8Encoding]::new($false);" ^
  "$b = [System.IO.File]::ReadAllText($bootstrap);" ^
  "$d = [System.IO.File]::ReadAllText($disabler);" ^
  "[System.IO.File]::Copy($bootstrap, ($bootstrap + '.black-screen-backup'), $true);" ^
  "[System.IO.File]::Copy($disabler, ($disabler + '.black-screen-backup'), $true);" ^
  "$pattern = '(?ms)\r?\n\s*if \(setup\.GetComponent<ComfortVignetteDisabler>\(\) == null\)\r?\n\s*setup\.AddComponent<ComfortVignetteDisabler>\(\);';" ^
  "$newB = [regex]::Replace($b, $pattern, '');" ^
  "$newD = $d.Replace('Destroy(gameObject);', 'Destroy(this);');" ^
  "if ($newB -eq $b -and $b.Contains('setup.AddComponent<ComfortVignetteDisabler>();')) { Write-Host 'ERROR: Could not remove the bad component attachment.' -ForegroundColor Red; exit 2 };" ^
  "if ($newD -eq $d -and $d.Contains('Destroy(gameObject);')) { Write-Host 'ERROR: Could not fix duplicate protection.' -ForegroundColor Red; exit 3 };" ^
  "[System.IO.File]::WriteAllText($bootstrap, $newB, $utf8);" ^
  "[System.IO.File]::WriteAllText($disabler, $newD, $utf8);" ^
  "Write-Host 'Black-screen code fix installed successfully.' -ForegroundColor Green;"

set "RESULT=%ERRORLEVEL%"
echo.

if not "%RESULT%"=="0" (
    echo Fix failed with error code %RESULT%.
    echo Send a screenshot of this window.
    echo.
    pause
    exit /b %RESULT%
)

echo IMPORTANT:
echo 1. Return to Unity and wait for compilation.
echo 2. Make sure Play Mode is OFF.
echo 3. Click Doctor Who VR ^> Rebuild Clean Portal Prototype.
echo 4. Confirm the rebuild.
echo 5. Enter Play Mode again.
echo.
echo The XR Interaction Setup should remain in the Hierarchy,
echo and the Game view should no longer say No cameras rendering.
echo.
pause
