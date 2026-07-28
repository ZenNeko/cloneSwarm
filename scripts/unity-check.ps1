# scripts/unity-check.ps1
# Unity batchmode compile check for the multi-agent loop.
#
#   .\scripts\unity-check.ps1                 # compile check
#   .\scripts\unity-check.ps1 -Audit          # compile + ProjectAutomatedAuditor.PerformFullAudit
#   .\scripts\unity-check.ps1 -UnityPath "..."
#
# Exit codes: 0 = clean, 1 = C# errors / failure, 2 = editor locked or Unity not found.

param (
    [string]$UnityPath = "",
    [string]$ProjectPath = (Resolve-Path (Join-Path $PSScriptRoot "..")).Path,
    [string]$LogPath = "",
    [switch]$Audit,
    [switch]$DryRun   # resolve the editor + check the lock, then stop. Cheap env sanity check.
)

$ErrorActionPreference = "Stop"

if ([string]::IsNullOrWhiteSpace($LogPath)) {
    $LogPath = Join-Path $ProjectPath "scratch\build.log"
}
$scratchDir = Split-Path -Path $LogPath -Parent
$errorsPath = Join-Path $scratchDir "errors.txt"

Write-Host "[unity-check] project: $ProjectPath" -ForegroundColor Cyan

# ---------------------------------------------------------------- 1. locate Unity
if ([string]::IsNullOrWhiteSpace($UnityPath)) {
    if ($env:UNITY_PATH -and (Test-Path $env:UNITY_PATH)) {
        $UnityPath = $env:UNITY_PATH
    } else {
        # Version the project actually wants.
        $wanted = $null
        $pv = Join-Path $ProjectPath "ProjectSettings\ProjectVersion.txt"
        if (Test-Path $pv) {
            $m = Select-String -Path $pv -Pattern '^m_EditorVersion:\s*(\S+)' | Select-Object -First 1
            if ($m) { $wanted = $m.Matches[0].Groups[1].Value }
        }
        if ($wanted) { Write-Host "[unity-check] project wants Unity $wanted" -ForegroundColor Gray }

        $installs = @()

        # Hub install roots: the default one plus whatever the user relocated to.
        $roots = @("C:\Program Files\Unity\Hub\Editor")
        $secondary = Join-Path $env:APPDATA "UnityHub\secondaryInstallPath.json"
        if (Test-Path $secondary) {
            $raw = (Get-Content $secondary -Raw).Trim().Trim('"')
            if ($raw) { $roots += $raw.Replace('\\', '\') }
        }
        foreach ($root in $roots) {
            if (Test-Path $root) {
                Get-ChildItem $root -Directory -ErrorAction SilentlyContinue | ForEach-Object {
                    $exe = Join-Path $_.FullName "Editor\Unity.exe"
                    if (Test-Path $exe) { $installs += [pscustomobject]@{ Version = $_.Name; Path = $exe } }
                }
            }
        }

        # Editors added to the Hub by hand can live anywhere, so also read editors-v2.json.
        # Parsed by regex on purpose: that file has keys differing only in case
        # ("preSelected"/"preselected") which makes ConvertFrom-Json throw outright.
        $editorsJson = Join-Path $env:APPDATA "UnityHub\editors-v2.json"
        if (Test-Path $editorsJson) {
            $json = Get-Content $editorsJson -Raw
            $rx = '"version"\s*:\s*"([^"]+)"\s*,\s*"location"\s*:\s*\[\s*"((?:[^"\\]|\\.)*)"'
            foreach ($mm in [regex]::Matches($json, $rx)) {
                $installs += [pscustomobject]@{
                    Version = $mm.Groups[1].Value
                    Path    = $mm.Groups[2].Value.Replace('\\', '\')
                }
            }
        }

        $installs = $installs | Where-Object { Test-Path $_.Path } |
                    Sort-Object Path -Unique

        # Exact version match wins; otherwise highest version available.
        $pick = $installs | Where-Object { $_.Version -eq $wanted } | Select-Object -First 1
        if (-not $pick) {
            $pick = $installs | Sort-Object Version -Descending | Select-Object -First 1
            if ($pick -and $wanted) {
                Write-Host "[unity-check] WARNING: $wanted not installed, falling back to $($pick.Version)." -ForegroundColor Yellow
                Write-Host "[unity-check] A different editor version will re-import the whole project (slow) and may rewrite ProjectVersion.txt." -ForegroundColor Yellow
            }
        }
        if ($pick) { $UnityPath = $pick.Path }
    }
}

if ([string]::IsNullOrWhiteSpace($UnityPath) -or !(Test-Path $UnityPath)) {
    Write-Host "[unity-check] Unity.exe not found." -ForegroundColor Red
    Write-Host "[unity-check] Fix: `$env:UNITY_PATH = 'D:\path\to\Editor\Unity.exe'  (or pass -UnityPath)" -ForegroundColor Yellow
    exit 2
}
Write-Host "[unity-check] editor:  $UnityPath" -ForegroundColor Green

# ---------------------------------------------------------------- 2. editor lock
# Batchmode cannot open a project that the GUI editor already has open.
$lockFile = Join-Path $ProjectPath "Temp\UnityLockfile"
if (Test-Path $lockFile) {
    $held = $false
    try {
        $fs = [System.IO.File]::Open($lockFile, 'Open', 'ReadWrite', 'None')
        $fs.Close()
    } catch { $held = $true }
    if ($held) {
        Write-Host "[unity-check] Unity Editor has this project open (Temp\UnityLockfile is locked)." -ForegroundColor Red
        Write-Host "[unity-check] Close the Editor and re-run, or just let the Editor recompile and read its Console." -ForegroundColor Yellow
        exit 2
    }
}

if ($DryRun) {
    Write-Host "[unity-check] dry run OK - editor resolved, project not locked. Nothing executed." -ForegroundColor Green
    exit 0
}

# ---------------------------------------------------------------- 3. run
if (!(Test-Path $scratchDir)) { New-Item -ItemType Directory -Path $scratchDir -Force | Out-Null }
Remove-Item $LogPath, $errorsPath -ErrorAction SilentlyContinue

$unityArgs = @(
    "-batchmode", "-nographics", "-quit",
    "-projectPath", "`"$ProjectPath`"",
    "-logFile", "`"$LogPath`""
)
if ($Audit) { $unityArgs += @("-executeMethod", "ProjectAutomatedAuditor.PerformFullAudit") }

# Measured on this project 2026-07-28: cold (empty Library) 62 min, warm ~41 s.
Write-Host "[unity-check] running batchmode (~40s warm; up to an hour if Library/ is cold)..." -ForegroundColor Gray
$sw = [Diagnostics.Stopwatch]::StartNew()
$proc = Start-Process -FilePath $UnityPath -ArgumentList $unityArgs -Wait -PassThru -NoNewWindow
$sw.Stop()
Write-Host ("[unity-check] exit {0} in {1:n0}s" -f $proc.ExitCode, $sw.Elapsed.TotalSeconds) -ForegroundColor Gray

# ---------------------------------------------------------------- 4. parse
if (!(Test-Path $LogPath)) {
    Write-Host "[unity-check] no log produced at $LogPath" -ForegroundColor Red
    exit 1
}

# Only two things count as failure: C# compile errors, and the auditor saying no.
# Deliberately NOT matching "Assertion failed" - batchmode -nographics spams
# "Assertion failed on expression: 'res'" plus a native stack trace on a clean project,
# which buries the real errors and produces false failures.
$compileErrors = Select-String -Path $LogPath -Pattern 'error CS\d+'
$auditErrors   = Select-String -Path $LogPath -Pattern '\[AutomatedAudit\].*(?i:missing|error|failed)'

if ($compileErrors -or $auditErrors) {
    $lines = @()
    $lines += ($compileErrors | ForEach-Object { $_.Line.Trim() })
    $lines += ($auditErrors   | ForEach-Object { $_.Line.Trim() })
    $lines = $lines | Select-Object -Unique
    $lines | Set-Content -Path $errorsPath -Encoding UTF8

    Write-Host "[unity-check] FAILED - $($lines.Count) error line(s). Full list: $errorsPath" -ForegroundColor Red
    $lines | Select-Object -First 30 | ForEach-Object { Write-Host "  $_" -ForegroundColor Red }
    if ($lines.Count -gt 30) { Write-Host "  ... $($lines.Count - 30) more in $errorsPath" -ForegroundColor DarkRed }
    exit 1
}

if ($proc.ExitCode -ne 0) {
    Write-Host "[unity-check] no C# errors, but Unity exited $($proc.ExitCode). Tail of log:" -ForegroundColor Yellow
    Get-Content $LogPath -Tail 25 | ForEach-Object { Write-Host "  $_" -ForegroundColor DarkYellow }
    exit 1
}

$assertCount = (Select-String -Path $LogPath -Pattern 'Assertion failed on expression' | Measure-Object).Count
if ($assertCount -gt 0) {
    Write-Host "[unity-check] note: $assertCount batchmode assertion(s) in the log - normal noise under -nographics, not treated as failure." -ForegroundColor DarkGray
}
Write-Host "[unity-check] OK - compilation clean, 0 C# errors." -ForegroundColor Green
exit 0
