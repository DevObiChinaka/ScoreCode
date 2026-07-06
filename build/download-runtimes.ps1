<#
.SYNOPSIS
    Downloads portable runtimes for ScoreCode bundled distribution.

.DESCRIPTION
    Downloads Python, Node.js, OpenJDK, and MinGW (g++) as portable/embeddable
    distributions and stages them into ScoreCode.Client\runtimes\ with the exact
    folder structure that RuntimePaths.cs expects.

    Run this script ONCE before building the Inno Setup installer.
    The runtimes/ folder is .gitignored — only this script is version-controlled.

.EXAMPLE
    .\build\download-runtimes.ps1
    .\build\download-runtimes.ps1 -SkipExisting
#>

param(
    [switch]$SkipExisting,  # Skip downloads that already exist
    [switch]$Force          # Re-download everything
)

$ErrorActionPreference = 'Stop'

# ── Configuration ─────────────────────────────────────────────────────────────
$RuntimesDir = Join-Path $PSScriptRoot '..\ScoreCode.Client\runtimes'
$TempDir     = Join-Path $env:TEMP 'ScoreCode-runtime-downloads'

$Runtimes = @(
    @{
        Name       = 'Python 3.12'
        Url        = 'https://www.python.org/ftp/python/3.12.8/python-3.12.8-embed-amd64.zip'
        TargetDir  = 'python-3.12'
        ZipName    = 'python-3.12.8-embed-amd64.zip'
        # Python embeddable extracts flat (python.exe + DLLs at root)
        FlatExtract = $true
    },
    @{
        Name       = 'Node.js 20 LTS'
        Url        = 'https://nodejs.org/dist/v20.18.1/node-v20.18.1-win-x64.zip'
        TargetDir  = 'node-20'
        ZipName    = 'node-v20.18.1-win-x64.zip'
        # Node.js extracts to node-v20.18.1-win-x64/ subfolder
        StripRoot  = 'node-v20.18.1-win-x64'
    },
    @{
        Name       = 'Microsoft OpenJDK 21'
        Url        = 'https://aka.ms/download-jdk/microsoft-jdk-21.0.5-windows-x64.zip'
        TargetDir  = 'jdk-21'
        ZipName    = 'microsoft-jdk-21.0.5-windows-x64.zip'
        # JDK extracts to jdk-21.0.5+11/ subfolder
        StripRoot  = 'jdk-21.0.5+11'
    },
    @{
        Name       = 'MinGW g++ (WinLibs)'
        Url        = 'https://github.com/brechtsanders/winlibs_mingw/releases/download/14.2.0posix-19.1.7-12.0.0-ucrt-r3/winlibs-x86_64-posix-seh-gcc-14.2.0-mingw-w64ucrt-12.0.0-r3.zip'
        TargetDir  = 'mingw64'
        ZipName    = 'winlibs-mingw64.zip'
        # WinLibs extracts to mingw64/ subfolder
        StripRoot  = 'mingw64'
    }
)

# ── Functions ─────────────────────────────────────────────────────────────────

function Download-File {
    param([string]$Url, [string]$OutFile)
    Write-Host "  Downloading: $Url" -ForegroundColor DarkGray
    Write-Host "  To: $OutFile"
    $ProgressPreference = 'SilentlyContinue'  # Massively speeds up Invoke-WebRequest
    Invoke-WebRequest -Uri $Url -OutFile $OutFile -UseBasicParsing
    $ProgressPreference = 'Continue'
    $size = [math]::Round((Get-Item $OutFile).Length / 1MB, 1)
    Write-Host "  Downloaded: $size MB" -ForegroundColor Green
}

function Extract-Runtime {
    param(
        [hashtable]$Config,
        [string]$ZipPath,
        [string]$DestDir
    )

    $targetPath = Join-Path $DestDir $Config.TargetDir

    if ($SkipExisting -and (Test-Path $targetPath)) {
        Write-Host "  Skipping (already exists): $targetPath" -ForegroundColor Yellow
        return
    }

    # Clean target if it exists
    if (Test-Path $targetPath) {
        Remove-Item $targetPath -Recurse -Force
    }

    # Extract to temp location
    $extractTemp = Join-Path $TempDir "extract_$($Config.TargetDir)"
    if (Test-Path $extractTemp) { Remove-Item $extractTemp -Recurse -Force }

    Write-Host "  Extracting..." -ForegroundColor DarkGray
    Expand-Archive -Path $ZipPath -DestinationPath $extractTemp -Force

    if ($Config.FlatExtract) {
        # Files are directly in the extract folder
        New-Item -ItemType Directory -Path $targetPath -Force | Out-Null
        Get-ChildItem $extractTemp | Move-Item -Destination $targetPath -Force
    }
    elseif ($Config.StripRoot) {
        # Files are inside a subfolder — move the subfolder contents up
        $innerDir = Get-ChildItem $extractTemp -Directory | Select-Object -First 1
        if ($innerDir) {
            Move-Item -Path $innerDir.FullName -Destination $targetPath -Force
        }
        else {
            # Fallback: no subfolder found, use as-is
            Move-Item -Path $extractTemp -Destination $targetPath -Force
        }
    }
    else {
        Move-Item -Path $extractTemp -Destination $targetPath -Force
    }

    # Cleanup temp extraction
    if (Test-Path $extractTemp) { Remove-Item $extractTemp -Recurse -Force }

    # Verify the expected executable exists
    $exeChecks = @{
        'python-3.12' = 'python.exe'
        'node-20'     = 'node.exe'
        'jdk-21'      = 'bin\java.exe'
        'mingw64'     = 'bin\g++.exe'
    }

    $expectedExe = Join-Path $targetPath $exeChecks[$Config.TargetDir]
    if (Test-Path $expectedExe) {
        Write-Host "  Verified: $expectedExe" -ForegroundColor Green
    }
    else {
        Write-Host "  WARNING: Expected executable not found: $expectedExe" -ForegroundColor Red
    }
}

# ── Main ──────────────────────────────────────────────────────────────────────

Write-Host ""
Write-Host "╔══════════════════════════════════════════════════╗" -ForegroundColor Cyan
Write-Host "║   ScoreCode — Runtime Bundler                    ║" -ForegroundColor Cyan
Write-Host "╚══════════════════════════════════════════════════╝" -ForegroundColor Cyan
Write-Host ""

# Create directories
New-Item -ItemType Directory -Path $RuntimesDir -Force | Out-Null
New-Item -ItemType Directory -Path $TempDir -Force | Out-Null

Write-Host "Runtimes directory: $RuntimesDir"
Write-Host "Temp directory: $TempDir"
Write-Host ""

foreach ($rt in $Runtimes) {
    Write-Host "── $($rt.Name) ──" -ForegroundColor Cyan

    $zipPath = Join-Path $TempDir $rt.ZipName

    # Download if needed
    if ($Force -or -not (Test-Path $zipPath)) {
        Download-File -Url $rt.Url -OutFile $zipPath
    }
    else {
        Write-Host "  Using cached download: $zipPath" -ForegroundColor Yellow
    }

    # Extract
    Extract-Runtime -Config $rt -ZipPath $zipPath -DestDir $RuntimesDir
    Write-Host ""
}

# Create marker file
$markerPath = Join-Path $RuntimesDir 'scorecode-runtimes.marker'
@"
ScoreCode Bundled Runtimes
Created: $(Get-Date -Format 'yyyy-MM-dd HH:mm:ss')
Python:  3.12.8 (embeddable)
Node.js: 20.18.1 LTS
JDK:     21.0.5 (Microsoft OpenJDK)
MinGW:   14.2.0 (WinLibs POSIX/UCRT)
"@ | Set-Content $markerPath -Encoding UTF8

Write-Host "── Summary ──" -ForegroundColor Cyan
Write-Host "  Marker file: $markerPath" -ForegroundColor Green

# Show sizes
$totalSize = 0
foreach ($rt in $Runtimes) {
    $dir = Join-Path $RuntimesDir $rt.TargetDir
    if (Test-Path $dir) {
        $size = [math]::Round((Get-ChildItem $dir -Recurse | Measure-Object -Property Length -Sum).Sum / 1MB, 1)
        $totalSize += $size
        Write-Host "  $($rt.Name): $size MB" -ForegroundColor DarkGray
    }
}
Write-Host "  Total: $totalSize MB" -ForegroundColor White
Write-Host ""
Write-Host "Done! Runtimes are ready for Inno Setup packaging." -ForegroundColor Green
Write-Host ""
