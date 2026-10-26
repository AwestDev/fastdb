@echo off
setlocal EnableExtensions

cd /d "%~dp0" || goto :fail

set "ROOT=%~dp0"
set "OUT=%ROOT%publish\FastDbExplorer"
set "ZIP=%ROOT%publish\FastDbExplorer-win-x64.zip"
set "APP_PROJ=src\FastDbExplorer.Wpf\FastDbExplorer.Wpf.csproj"
set "TEST_PROJ=tests\FastDbExplorer.Tests\FastDbExplorer.Tests.csproj"

echo.
echo ============================================================
echo FastDbExplorer - Windows x64 self-contained publish
echo ============================================================
echo IMPORTANT: Run this script on an INTERNET-CONNECTED build PC.
echo The destination PC can be offline; it should run the ZIP output,
echo not build the source project.
echo.

if not exist "%APP_PROJ%" (
    echo ERROR: App project not found: %APP_PROJ%
    goto :fail
)
if not exist "%TEST_PROJ%" (
    echo ERROR: Test project not found: %TEST_PROJ%
    goto :fail
)

if not exist "src\FastDbExplorer.Wpf\Assets\Map\fonts\NotoSans\0-255.pbf" (
    echo Map assets are missing. They must be downloaded on a connected PC.
    if not exist "setup-map-assets.cmd" (
        echo ERROR: setup-map-assets.cmd was not found.
        goto :fail
    ) else (
        call setup-map-assets.cmd nopause
        if errorlevel 1 goto :map_failed
    )
)
if not exist "src\FastDbExplorer.Wpf\Assets\Map\fonts\NotoSans\0-255.pbf" goto :map_failed

where dotnet >nul 2>&1
if errorlevel 1 (
    echo ERROR: .NET SDK was not found. Install the SDK required by this project.
    goto :fail
)

echo [1/5] Restoring test dependencies from NuGet...
dotnet restore "%TEST_PROJ%" --nologo
if errorlevel 1 goto :nuget_failed

echo [2/5] Restoring app dependencies for win-x64 from NuGet...
dotnet restore "%APP_PROJ%" -r win-x64 --nologo
if errorlevel 1 goto :nuget_failed

echo [3/5] Running tests...
dotnet test "%TEST_PROJ%" -c Release --no-restore --nologo
if errorlevel 1 goto :fail

echo [4/5] Publishing self-contained win-x64...
if exist "%OUT%" rmdir /s /q "%OUT%"
dotnet publish "%APP_PROJ%" -c Release -r win-x64 --self-contained true --no-restore -o "%OUT%" --nologo
if errorlevel 1 goto :fail

if not exist "%ROOT%publish" mkdir "%ROOT%publish"
echo [5/5] Creating ZIP...
if exist "%ZIP%" del /q "%ZIP%"
powershell -NoProfile -ExecutionPolicy Bypass -Command "Compress-Archive -Path '%OUT%\*' -DestinationPath '%ZIP%' -Force"
if errorlevel 1 goto :fail

echo.
echo SUCCESS.
echo Portable folder: %OUT%
echo ZIP package:    %ZIP%
echo Copy the ZIP to the offline PC, extract it, and run FastDbExplorer.exe.
echo The offline PC does not need NuGet or internet to run this published app.
pause
exit /b 0

:nuget_failed
echo.
echo ERROR: NuGet restore failed. This is a BUILD-TIME dependency problem.
echo If this PC is offline or cannot resolve api.nuget.org, this script cannot
echo download missing packages such as Microsoft.Data.Sqlite or xunit.
echo Run publish.cmd on an internet-connected build PC, then copy the ZIP to
echo the offline destination PC. Do not copy the source project and try to
echo publish it on the offline PC unless you first prepare a complete local NuGet feed.
goto :fail

:map_failed
echo.
echo ERROR: Required map assets are missing.
echo Download them on an internet-connected PC before publishing.
goto :fail

:fail
echo.
echo FAILED. No new publish ZIP was created.
pause
exit /b 1
