$ErrorActionPreference = "Stop"

$ProjectRoot = Split-Path -Parent $MyInvocation.MyCommand.Path
$TargetFile = Join-Path $ProjectRoot "Assets\DoctorWhoVR\Editor\PortalProjectBootstrap.cs"

Write-Host ""
Write-Host "Doctor Who VR - Prefab Error Fix" -ForegroundColor Green
Write-Host "Project: $ProjectRoot"
Write-Host ""

if (-not (Test-Path $TargetFile)) {
    Write-Host "Could not find:" -ForegroundColor Red
    Write-Host $TargetFile -ForegroundColor Red
    Write-Host ""
    Write-Host "Put this fix directly beside Assets, Packages, ProjectSettings, and .git." -ForegroundColor Yellow
    Read-Host "Press Enter to close"
    exit 1
}

$content = [System.IO.File]::ReadAllText($TargetFile)

$oldLine = "UnityEngine.Object.DestroyImmediate(child.gameObject);"
$newLine = "child.gameObject.SetActive(false);"

if ($content.Contains($newLine)) {
    Write-Host "The fix is already installed." -ForegroundColor Green
    Read-Host "Press Enter to close"
    exit 0
}

if (-not $content.Contains($oldLine)) {
    Write-Host "The expected error line was not found, so no file was changed." -ForegroundColor Red
    Write-Host "Send ChatGPT the current PortalProjectBootstrap.cs file or Console error." -ForegroundColor Yellow
    Read-Host "Press Enter to close"
    exit 1
}

$backupFile = "$TargetFile.prefab-error-backup"
[System.IO.File]::Copy($TargetFile, $backupFile, $true)

$replacement = @"
// A nested object inside the XR prefab instance cannot be destroyed directly.
// Disabling it creates a safe scene override and permanently removes the
// locomotion tunneling/dark-edge effect from the generated scene.
child.gameObject.SetActive(false);
"@

$content = $content.Replace($oldLine, $replacement.TrimEnd())
[System.IO.File]::WriteAllText(
    $TargetFile,
    $content,
    New-Object System.Text.UTF8Encoding($false)
)

Write-Host "Fixed PortalProjectBootstrap.cs successfully." -ForegroundColor Green
Write-Host "Backup created at:" -ForegroundColor Cyan
Write-Host $backupFile
Write-Host ""
Write-Host "Return to Unity and wait for it to compile." -ForegroundColor Cyan
Write-Host "Then use: Doctor Who VR > Rebuild Clean Portal Prototype" -ForegroundColor Cyan
Write-Host ""
Write-Host "Your auto-sync window should upload this fix to chatgpt-sync." -ForegroundColor Cyan
Read-Host "Press Enter to close"
