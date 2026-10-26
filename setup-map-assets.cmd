@echo off
setlocal
cd /d "%~dp0"

echo Downloading map engine files (one time, needs internet)...
powershell -NoProfile -ExecutionPolicy Bypass -File "%~dp0setup-map-assets.ps1"
if errorlevel 1 (
  echo.
  echo FAILED to download. Check your internet connection and run this file again.
  if "%~1"=="" pause
  exit /b 1
)
if "%~1"=="" pause
exit /b 0
