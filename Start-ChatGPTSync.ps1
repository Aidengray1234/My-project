param(
    [int]$PollSeconds = 5,
    [int]$QuietSeconds = 15,
    [string]$SyncBranch = "chatgpt-sync"
)

$ErrorActionPreference = "Stop"
$ProjectPath = Split-Path -Parent $MyInvocation.MyCommand.Path
Set-Location $ProjectPath

function Write-Info([string]$Message) {
    Write-Host "[DoctorWhoVR Sync] $Message" -ForegroundColor Cyan
}

function Write-Warn([string]$Message) {
    Write-Host "[DoctorWhoVR Sync] $Message" -ForegroundColor Yellow
}

function Write-Fail([string]$Message) {
    Write-Host "[DoctorWhoVR Sync] $Message" -ForegroundColor Red
}

function ConvertTo-NativeArgument([string]$Value) {
    if ($null -eq $Value) {
        return '""'
    }

    # Quote every argument and escape embedded backslashes/quotes for Windows.
    $escaped = $Value -replace '(\\*)"', '$1$1\"'
    $escaped = $escaped -replace '(\\+)$', '$1$1'
    return '"' + $escaped + '"'
}

function Invoke-Git {
    param(
        [Parameter(Mandatory = $true)]
        [string[]]$Arguments,
        [switch]$AllowFailure
    )

    $psi = New-Object System.Diagnostics.ProcessStartInfo
    $psi.FileName = "git"
    $psi.WorkingDirectory = $ProjectPath
    $psi.UseShellExecute = $false
    $psi.RedirectStandardOutput = $true
    $psi.RedirectStandardError = $true
    $psi.CreateNoWindow = $true
    $psi.Arguments = (($Arguments | ForEach-Object { ConvertTo-NativeArgument $_ }) -join " ")

    $process = New-Object System.Diagnostics.Process
    $process.StartInfo = $psi

    if (-not $process.Start()) {
        throw "Could not start Git."
    }

    $stdout = $process.StandardOutput.ReadToEnd()
    $stderr = $process.StandardError.ReadToEnd()
    $process.WaitForExit()

    $combined = @()
    if (-not [string]::IsNullOrWhiteSpace($stdout)) {
        $combined += $stdout.TrimEnd()
    }
    if (-not [string]::IsNullOrWhiteSpace($stderr)) {
        $combined += $stderr.TrimEnd()
    }

    $text = ($combined -join "`n")
    $exitCode = $process.ExitCode
    $process.Dispose()

    if (-not $AllowFailure -and $exitCode -ne 0) {
        throw "git $($Arguments -join ' ') failed with exit code $exitCode`n$text"
    }

    return [pscustomobject]@{
        ExitCode = $exitCode
        Output = $text
    }
}

function Test-CommandExists([string]$Name) {
    return $null -ne (Get-Command $Name -ErrorAction SilentlyContinue)
}

function Get-WorkingStatus {
    $result = Invoke-Git -Arguments @("status", "--porcelain=v1", "--untracked-files=all")
    return $result.Output.Trim()
}

function Test-StagedChanges {
    $result = Invoke-Git -Arguments @("diff", "--cached", "--quiet") -AllowFailure
    return $result.ExitCode -ne 0
}

function Test-ForSecrets {
    $diffResult = Invoke-Git -Arguments @("diff", "--cached", "--unified=0") -AllowFailure
    $diffText = $diffResult.Output

    $patterns = @(
        "ghp_[A-Za-z0-9]{20,}",
        "github_pat_[A-Za-z0-9_]{20,}",
        "sk-[A-Za-z0-9_-]{20,}",
        "AKIA[0-9A-Z]{16}",
        "-----BEGIN [A-Z ]*PRIVATE KEY-----"
    )

    foreach ($pattern in $patterns) {
        if ($diffText -match $pattern) {
            return $true
        }
    }

    return $false
}

function Ensure-SyncBranch {
    $currentResult = Invoke-Git -Arguments @("branch", "--show-current")
    $currentBranch = $currentResult.Output.Trim()

    if ([string]::IsNullOrWhiteSpace($currentBranch)) {
        throw "The repository is not currently on a normal Git branch."
    }

    if ($currentBranch -eq $SyncBranch) {
        Write-Info "Already on safe sync branch '$SyncBranch'."
        return
    }

    $existsResult = Invoke-Git -Arguments @(
        "show-ref",
        "--verify",
        "--quiet",
        "refs/heads/$SyncBranch"
    ) -AllowFailure

    if ($existsResult.ExitCode -eq 0) {
        Write-Info "Switching from '$currentBranch' to existing branch '$SyncBranch'..."
        $switchResult = Invoke-Git -Arguments @("switch", $SyncBranch)
        if (-not [string]::IsNullOrWhiteSpace($switchResult.Output)) {
            Write-Info $switchResult.Output
        }
    }
    else {
        Write-Info "Creating safe sync branch '$SyncBranch' from '$currentBranch'..."
        $switchResult = Invoke-Git -Arguments @("switch", "-c", $SyncBranch)
        if (-not [string]::IsNullOrWhiteSpace($switchResult.Output)) {
            Write-Info $switchResult.Output
        }
    }
}

function Push-CurrentBranch {
    $branchResult = Invoke-Git -Arguments @("branch", "--show-current")
    $branch = $branchResult.Output.Trim()

    $upstreamResult = Invoke-Git -Arguments @(
        "rev-parse",
        "--abbrev-ref",
        "--symbolic-full-name",
        "@{u}"
    ) -AllowFailure

    if ($upstreamResult.ExitCode -ne 0) {
        Write-Info "Publishing branch '$branch' to GitHub..."
        $pushResult = Invoke-Git -Arguments @("push", "-u", "origin", $branch)
    }
    else {
        $pushResult = Invoke-Git -Arguments @("push")
    }

    if (-not [string]::IsNullOrWhiteSpace($pushResult.Output)) {
        Write-Info $pushResult.Output
    }
}

function Sync-Project {
    Write-Info "Changes are stable. Preparing a GitHub update..."

    Invoke-Git -Arguments @("add", "-A") | Out-Null

    if (-not (Test-StagedChanges)) {
        Write-Info "Nothing needed to be uploaded."
        return $true
    }

    if (Test-ForSecrets) {
        Invoke-Git -Arguments @("reset") | Out-Null
        Write-Fail "UPLOAD BLOCKED: a token, API key, or private key pattern was detected."
        Write-Fail "Remove the secret from the project before syncing."
        return $false
    }

    $timestamp = Get-Date -Format "yyyy-MM-dd HH:mm:ss"
    $commitResult = Invoke-Git -Arguments @(
        "commit",
        "-m",
        "Auto-sync Unity project $timestamp"
    ) -AllowFailure

    if ($commitResult.ExitCode -ne 0) {
        Write-Warn "Commit was not created:"
        Write-Warn $commitResult.Output
        return $false
    }

    if (-not [string]::IsNullOrWhiteSpace($commitResult.Output)) {
        Write-Info $commitResult.Output
    }

    try {
        Push-CurrentBranch
        Write-Host ""
        Write-Host "Uploaded successfully at $timestamp" -ForegroundColor Green
        Write-Host "I can now inspect branch: $SyncBranch" -ForegroundColor Green
        Write-Host ""
        return $true
    }
    catch {
        Write-Fail "The commit was created locally, but GitHub push failed:"
        Write-Fail $_.Exception.Message
        return $false
    }
}

Write-Host ""
Write-Host "Doctor Who VR - Automatic GitHub Sync v2" -ForegroundColor Green
Write-Host "Project: $ProjectPath"
Write-Host "Press Ctrl+C to stop."
Write-Host ""

if (-not (Test-CommandExists "git")) {
    Write-Fail "Git was not found in PATH."
    Read-Host "Press Enter to close"
    exit 1
}

if (-not (Test-Path (Join-Path $ProjectPath ".git"))) {
    Write-Fail "This file is not in the Unity project's Git root."
    Write-Fail "Place it beside Assets, Packages, ProjectSettings, and .git."
    Read-Host "Press Enter to close"
    exit 1
}

$originResult = Invoke-Git -Arguments @("remote", "get-url", "origin") -AllowFailure
if ($originResult.ExitCode -ne 0) {
    Write-Fail "No Git remote named 'origin' exists."
    Read-Host "Press Enter to close"
    exit 1
}

try {
    Ensure-SyncBranch
}
catch {
    Write-Fail $_.Exception.Message
    Read-Host "Press Enter to close"
    exit 1
}

Write-Info "Watching the project."
Write-Info "Changes upload after $QuietSeconds seconds without more edits."
Write-Info "Unity's Library, Temp, Logs, and UserSettings stay excluded by .gitignore."

$lastStatus = ""
$stableSince = Get-Date
$lastBlockedStatus = ""

while ($true) {
    try {
        $status = Get-WorkingStatus

        if ([string]::IsNullOrWhiteSpace($status)) {
            $lastStatus = ""
            $lastBlockedStatus = ""
            $stableSince = Get-Date
        }
        elseif ($status -ne $lastStatus) {
            $lastStatus = $status
            $stableSince = Get-Date
            Write-Info "Change detected. Waiting for Unity to finish writing files..."
        }
        else {
            $quietFor = ((Get-Date) - $stableSince).TotalSeconds

            if ($quietFor -ge $QuietSeconds -and $status -ne $lastBlockedStatus) {
                $success = Sync-Project

                if ($success) {
                    $lastStatus = ""
                    $lastBlockedStatus = ""
                    $stableSince = Get-Date
                }
                else {
                    $lastBlockedStatus = $status
                }
            }
        }
    }
    catch {
        Write-Warn $_.Exception.Message
    }

    Start-Sleep -Seconds $PollSeconds
}
