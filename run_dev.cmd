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

dotnet restore "src\AIExplorer\AIExplorer.csproj" ^
  --configfile "%NUGET_CONFIG%" ^
  -p:NuGetAudit=false || goto :restore_failed

dotnet run --project "src\AIExplorer\AIExplorer.csproj" ^
  -p:NuGetAudit=false ^
  --no-restore || goto :failed
exit /b 0

:restore_failed
echo.
echo NuGet package restore failed.
echo Check access to https://api.nuget.org/v3/index.json
goto :failed

:failed
echo.
echo Development run failed.
pause
exit /b 1
