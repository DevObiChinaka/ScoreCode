@echo off
REM ScoreCode one-command launcher.
REM Runs run.ps1 while bypassing PowerShell's script execution policy.
REM Usage:  run           (full run)
REM         run -NoBuild   (skip build)
powershell -NoProfile -ExecutionPolicy Bypass -File "%~dp0run.ps1" %*
