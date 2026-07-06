#Requires -Version 5.1
<#
    ScoreCode - stop all running app processes.

    Kills the API, web portal and desktop client (plus their 'dotnet run' host
    processes), which frees the build locks and ports 5039 / 5080. Safe to run
    anytime - if nothing is running it just says so.

    Usage:  .\stop.cmd      (or  .\stop.ps1)
#>
[CmdletBinding()]
param([switch]$Quiet)

$killed = @()

# App host executables (these are what hold the DLL locks + ports).
foreach ($name in 'ScoreCode.Api', 'ScoreCode.Web', 'ScoreCode.Client') {
    Get-Process -Name $name -ErrorAction SilentlyContinue | ForEach-Object {
        $killed += "$($_.ProcessName) (PID $($_.Id))"
        Stop-Process -Id $_.Id -Force -ErrorAction SilentlyContinue
    }
}

# 'dotnet run --project ScoreCode.*.csproj' launcher hosts (matched by command
# line so we never touch unrelated dotnet / build processes).
Get-CimInstance Win32_Process -Filter "Name='dotnet.exe'" -ErrorAction SilentlyContinue |
    Where-Object { $_.CommandLine -and $_.CommandLine -match 'ScoreCode\.(Api|Web|Client)\.csproj' } |
    ForEach-Object {
        $killed += "dotnet run host (PID $($_.ProcessId))"
        Stop-Process -Id $_.ProcessId -Force -ErrorAction SilentlyContinue
    }

if (-not $Quiet) {
    if ($killed.Count -gt 0) {
        Write-Host "Stopped:" -ForegroundColor Green
        $killed | ForEach-Object { Write-Host "  $_" }
    } else {
        Write-Host "No ScoreCode processes were running." -ForegroundColor DarkGray
    }
}
