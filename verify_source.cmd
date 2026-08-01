@echo off
setlocal
cd /d "%~dp0"

set "NUGET_CONFIG=%~dp0NuGet.Config"

where dotnet >nul 2>nul
if errorlevel 1 (
    echo .NET 10 SDK is required.
    echo https://dotnet.microsoft.com/download/dotnet/10.0
    pause
    exit /b 1
)

dotnet --list-sdks 2>nul | findstr /b /c:"10." >nul
if errorlevel 1 (
    echo .NET 10 SDK was not found.
    echo Install the Windows x64 SDK, not only the runtime.
    echo https://dotnet.microsoft.com/download/dotnet/10.0
    pause
    exit /b 1
)

if not exist "%NUGET_CONFIG%" (
    echo NuGet.Config was not found:
    echo %NUGET_CONFIG%
    goto :failed
)

echo Running source preflight checks...
powershell -NoProfile -ExecutionPolicy Bypass ^
  -File "%~dp0tools\preflight.ps1" || goto :failed

echo Restoring packages from the project NuGet.Config...
dotnet restore "tests\AIExplorer.SmokeTests\AIExplorer.SmokeTests.csproj" ^
  --configfile "%NUGET_CONFIG%" ^
  --force-evaluate ^
  -p:NuGetAudit=false || goto :restore_failed

dotnet build "tests\AIExplorer.SmokeTests\AIExplorer.SmokeTests.csproj" ^
  -c Release ^
  -p:NuGetAudit=false ^
  --no-restore || goto :failed

dotnet "tests\AIExplorer.SmokeTests\bin\Release\net10.0-windows10.0.19041.0\AIExplorer.SmokeTests.dll" || goto :failed

echo.
echo Verification completed successfully.
exit /b 0

:restore_failed
echo.
echo NuGet package restore failed.
echo This project uses its own NuGet.Config and the official nuget.org v3 feed.
echo Check whether a firewall, proxy, security program, or DNS policy blocks api.nuget.org.
echo You can test the feed in a browser:
echo https://api.nuget.org/v3/index.json
goto :failed

:failed
echo.
echo Verification failed.
pause
exit /b 1
