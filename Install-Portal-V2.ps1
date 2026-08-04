$ErrorActionPreference = "Stop"

$ProjectRoot = Split-Path -Parent $MyInvocation.MyCommand.Path
$PayloadRoot = Join-Path $ProjectRoot "_PortalV2Payload"

$Files = @(
    "Assets\DoctorWhoVR\Scripts\Portals\Portal.cs",
    "Assets\DoctorWhoVR\Shaders\PortalSurface.shader"
)

Write-Host ""
Write-Host "Doctor Who VR - Portal Rendering V2" -ForegroundColor Green
Write-Host "Project: $ProjectRoot"
Write-Host ""

if (-not (Test-Path (Join-Path $ProjectRoot "Assets"))) {
    Write-Host "ERROR: This installer is not in the Unity project root." -ForegroundColor Red
    Write-Host "Place it beside Assets, Packages, ProjectSettings, and .git." -ForegroundColor Yellow
    Read-Host "Press Enter to close"
    exit 1
}

foreach ($RelativePath in $Files) {
    $Source = Join-Path $PayloadRoot $RelativePath
    $Destination = Join-Path $ProjectRoot $RelativePath

    if (-not (Test-Path $Source)) {
        throw "Missing payload file: $Source"
    }

    $DestinationFolder = Split-Path -Parent $Destination
    New-Item -ItemType Directory -Force -Path $DestinationFolder | Out-Null

    if (Test-Path $Destination) {
        Copy-Item $Destination "$Destination.portal-v1-backup" -Force
    }

    Copy-Item $Source $Destination -Force
    Write-Host "Installed: $RelativePath" -ForegroundColor Cyan
}

Write-Host ""
Write-Host "Portal V2 installed successfully." -ForegroundColor Green
Write-Host ""
Write-Host "Return to Unity and wait for script/shader compilation." -ForegroundColor Cyan
Write-Host "Do not rebuild the scene unless Unity reports missing references." -ForegroundColor Cyan
Write-Host "Clear the Console, then enter Play Mode and test the doorway." -ForegroundColor Cyan
Write-Host ""
Write-Host "The auto-sync window will upload these changes." -ForegroundColor Cyan
Read-Host "Press Enter to close"
