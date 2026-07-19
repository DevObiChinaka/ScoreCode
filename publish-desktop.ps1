#Requires -Version 5.1
<#
    Publishes the ScoreCode desktop client and drops a zipped Windows build where
    the web landing page serves it (ScoreCode.Web/wwwroot/downloads/).

    The build is a self-contained win-x64 folder (not a single file): the client
    loads the Monaco editor from loose asset files on disk, so those must ship
    alongside the exe. Students unzip and run ScoreCode.Client.exe — no .NET
    install needed. Windows 10/11 with the WebView2 runtime (preinstalled on
    current Windows) is required.

    Usage:
      .\publish-desktop.ps1                         # build with the current appsettings ServerUrl
      .\publish-desktop.ps1 -ApiUrl https://exams.myschool.edu   # point students at your API

    IMPORTANT: the default ServerUrl is http://localhost:5039, which only works
    on the same machine as the API. Pass -ApiUrl (your reachable API address) for
    a build you actually hand to students.
#>
[CmdletBinding()]
param(
    [string]$ApiUrl,
    [string]$Configuration = 'Release'
)

$ErrorActionPreference = 'Stop'
$root = $PSScriptRoot
Set-Location $root

$project   = Join-Path $root 'ScoreCode.Client\ScoreCode.Client.csproj'
$publishDir = Join-Path $root 'artifacts\desktop'
$downloads = Join-Path $root 'ScoreCode.Web\wwwroot\downloads'
$zipPath   = Join-Path $downloads 'ScoreCode-Windows.zip'

Write-Host "=== Publishing the desktop client (win-x64, self-contained) ===" -ForegroundColor Cyan
if (Test-Path $publishDir) { Remove-Item $publishDir -Recurse -Force }

& dotnet publish $project -c $Configuration -r win-x64 --self-contained true -o $publishDir --nologo
if ($LASTEXITCODE -ne 0) { throw "dotnet publish failed." }

# Optionally rewrite the API address the client talks to.
if ($ApiUrl) {
    $settingsPath = Join-Path $publishDir 'appsettings.json'
    if (Test-Path $settingsPath) {
        $json = Get-Content $settingsPath -Raw | ConvertFrom-Json
        $json.ServerUrl = $ApiUrl
        ($json | ConvertTo-Json -Depth 10) | Set-Content $settingsPath -Encoding utf8
        Write-Host "Set client ServerUrl to $ApiUrl" -ForegroundColor Green
    }
} else {
    Write-Host "WARNING: building with the default ServerUrl (localhost). Pass -ApiUrl for a build you give to students." -ForegroundColor Yellow
}

Write-Host "=== Zipping the build ===" -ForegroundColor Cyan
New-Item -ItemType Directory -Force -Path $downloads | Out-Null
if (Test-Path $zipPath) { Remove-Item $zipPath -Force }
Compress-Archive -Path (Join-Path $publishDir '*') -DestinationPath $zipPath

$sizeMb = [math]::Round((Get-Item $zipPath).Length / 1MB, 1)
Write-Host ""
Write-Host "Done. $zipPath ($sizeMb MB)" -ForegroundColor Green
Write-Host "The landing page's download button is now live." -ForegroundColor Green
