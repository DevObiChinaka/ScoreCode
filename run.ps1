#Requires -Version 5.1
<#
    ScoreCode - one-command launcher.

    Brings the whole system up from a single terminal:
      1. PostgreSQL   (starts the Docker container if 5432 isn't already up, waits until ready)
      2. Build        (dotnet build the solution, unless -NoBuild)
      3. API          (starts ScoreCode.Api on http://localhost:5039, waits until it's listening)
      4. Client       (runs the WPF app in the foreground; closing it tears everything down)

    Usage:
      .\run.ps1               # full run
      .\run.ps1 -NoBuild      # skip the build step (faster if already built)
      .\run.ps1 -StopDbOnExit # also stop the Postgres container when the app closes

    Tip: run  .\run.cmd  if PowerShell blocks scripts (it bypasses the execution policy).
#>
[CmdletBinding()]
param(
    [switch]$NoBuild,
    [switch]$StopDbOnExit
)

$ErrorActionPreference = 'Stop'
$root = $PSScriptRoot
Set-Location $root

# ---- Helpers --------------------------------------------------------------
function Write-Step([string]$msg) {
    Write-Host ""
    Write-Host "=== $msg ===" -ForegroundColor Cyan
}

function Test-Port([int]$port) {
    $client = New-Object System.Net.Sockets.TcpClient
    try {
        $async = $client.BeginConnect('127.0.0.1', $port, $null, $null)
        $ok = $async.AsyncWaitHandle.WaitOne(500)
        return ($ok -and $client.Connected)
    } catch {
        return $false
    } finally {
        $client.Close()
    }
}

function Wait-Port([int]$port, [int]$timeoutSec) {
    $sw = [System.Diagnostics.Stopwatch]::StartNew()
    while ($sw.Elapsed.TotalSeconds -lt $timeoutSec) {
        if (Test-Port $port) { return $true }
        Start-Sleep -Milliseconds 700
    }
    return $false
}

# Kill a process AND its children (dotnet run spawns the real app as a child).
function Stop-Tree([int]$processId) {
    try { & taskkill /PID $processId /T /F 2>$null | Out-Null } catch { }
}

$startedDb = $false
$apiProc = $null

try {
    # ---- 1. PostgreSQL ----------------------------------------------------
    Write-Step "PostgreSQL (localhost:5432)"
    if (Test-Port 5432) {
        Write-Host "PostgreSQL already reachable on 5432 - using it." -ForegroundColor Green
    } else {
        $docker = Get-Command docker -ErrorAction SilentlyContinue
        if (-not $docker) {
            throw "Nothing is listening on 5432 and Docker was not found. Start PostgreSQL yourself, or install Docker Desktop."
        }

        Write-Host "Starting the PostgreSQL container (docker compose up -d)..."
        & docker compose -f "docker-compose.yml" up -d
        if ($LASTEXITCODE -ne 0) { throw "docker compose failed to start the database." }
        $startedDb = $true

        Write-Host "Waiting for PostgreSQL to accept connections..."
        $ready = $false
        $sw = [System.Diagnostics.Stopwatch]::StartNew()
        while ($sw.Elapsed.TotalSeconds -lt 90) {
            & docker exec scorecode_db pg_isready -U sc -d scorecode 2>$null | Out-Null
            if ($LASTEXITCODE -eq 0) { $ready = $true; break }
            Start-Sleep -Seconds 2
        }
        if (-not $ready) { throw "PostgreSQL did not become ready within 90s." }
        Write-Host "PostgreSQL is ready." -ForegroundColor Green
    }

    # ---- 2. Build ---------------------------------------------------------
    if (-not $NoBuild) {
        Write-Step "Building the solution"
        & dotnet build "ScoreCode.slnx" -v minimal --nologo
        if ($LASTEXITCODE -ne 0) { throw "Build failed - fix the errors above and re-run." }
    }

    # ---- 3. API -----------------------------------------------------------
    Write-Step "API (http://localhost:5039)"
    if (Test-Port 5039) {
        Write-Host "Port 5039 already in use - reusing the running API." -ForegroundColor Yellow
    } else {
        New-Item -ItemType Directory -Force -Path "$root\logs" | Out-Null
        $env:ASPNETCORE_ENVIRONMENT = 'Development'

        $apiArgs = @(
            'run', '--project', 'ScoreCode.Api\ScoreCode.Api.csproj',
            '--no-launch-profile', '--urls', 'http://localhost:5039'
        )
        $apiProc = Start-Process -FilePath 'dotnet' -ArgumentList $apiArgs -PassThru `
            -WindowStyle Hidden `
            -RedirectStandardOutput "$root\logs\api.out.log" `
            -RedirectStandardError  "$root\logs\api.err.log"

        Write-Host "API starting (PID $($apiProc.Id)) - logs in .\logs\. Waiting for it to listen..."
        if (-not (Wait-Port 5039 120)) {
            throw "The API never started listening on 5039. Check .\logs\api.err.log (is PostgreSQL reachable?)."
        }
        Write-Host "API is up." -ForegroundColor Green
    }

    # ---- 4. Client (foreground) -------------------------------------------
    Write-Step "Launching the ScoreCode client - close the app window to shut everything down"
    $env:ASPNETCORE_ENVIRONMENT = 'Development'
    & dotnet run --project "ScoreCode.Client\ScoreCode.Client.csproj"
}
finally {
    Write-Step "Shutting down"

    if ($apiProc -and -not $apiProc.HasExited) {
        Write-Host "Stopping API (PID $($apiProc.Id))..."
        Stop-Tree $apiProc.Id
    }

    if ($StopDbOnExit -and $startedDb) {
        Write-Host "Stopping the PostgreSQL container..."
        & docker compose -f "docker-compose.yml" down | Out-Null
    } elseif ($startedDb) {
        Write-Host "PostgreSQL is left running. Stop it with:  docker compose down" -ForegroundColor DarkGray
    }

    Write-Host "Done." -ForegroundColor Green
}
