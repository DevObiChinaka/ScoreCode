@echo off
REM Stop all running ScoreCode processes (API, web portal, desktop client).
REM Frees build locks and ports 5039 / 5080.
powershell -NoProfile -ExecutionPolicy Bypass -File "%~dp0stop.ps1" %*
