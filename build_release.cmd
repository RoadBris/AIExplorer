@echo off
setlocal
cd /d "%~dp0"

set "NUGET_CONFIG=%~dp0NuGet.Config"
set "OUTPUT_DIR=dist\AIExplorer_v0.82.4-win-x64"
set "OUTPUT_ZIP=dist\AIExplorer_v0.82.4-win-x64-portable.zip"

where dotnet >nul 2>nul
if errorlevel 1 (
    echo .NET 10 SDK is required.
    echo https://dotnet.microsoft.com/download/dotnet/10.0
    pause
    exit /b 1
)

call "verify_source.cmd" || exit /b 1

powershell -NoProfile -ExecutionPolicy Bypass ^
  -Command "$tokens=$null; $errors=$null; [void][System.Management.Automation.Language.Parser]::ParseFile((Resolve-Path 'tools\prepare_ai_bundle.ps1').Path, [ref]$tokens, [ref]$errors); if ($errors.Count -gt 0) { Write-Error ($errors[0].Message); exit 1 }" ^
  || goto :failed

if not exist "dist" mkdir "dist"
if exist "%OUTPUT_DIR%" (
    rmdir /s /q "%OUTPUT_DIR%" >nul 2>nul
)
if exist "%OUTPUT_DIR%" (
    echo The existing portable app is running. Building to an updated folder.
    set "OUTPUT_DIR=dist\AIExplorer_v0.82.4-win-x64-updated"
    set "OUTPUT_ZIP=dist\AIExplorer_v0.82.4-win-x64-updated-portable.zip"
    if exist "dist\AIExplorer_v0.82.4-win-x64-updated" (
        rmdir /s /q "dist\AIExplorer_v0.82.4-win-x64-updated" >nul 2>nul
    )
)
if exist "%OUTPUT_DIR%" (
    echo The updated output folder is also in use.
    goto :failed
)
if exist "%OUTPUT_ZIP%" (
    del /q "%OUTPUT_ZIP%"
)

echo Restoring Windows x64 publish packages from the project NuGet.Config...
dotnet restore "src\AIExplorer\AIExplorer.csproj" ^
  -r win-x64 ^
  --configfile "%NUGET_CONFIG%" ^
  --force-evaluate ^
  -p:SelfContained=true ^
  -p:NuGetAudit=false || goto :restore_failed

dotnet publish "src\AIExplorer\AIExplorer.csproj" ^
  -c Release ^
  -r win-x64 ^
  --self-contained true ^
  --no-restore ^
  -p:PublishSingleFile=true ^
  -p:IncludeNativeLibrariesForSelfExtract=true ^
  -p:EnableCompressionInSingleFile=true ^
  -p:PublishTrimmed=false ^
  -p:NuGetAudit=false ^
  -p:DebugType=None ^
  -p:DebugSymbols=false ^
  -o "%OUTPUT_DIR%" || goto :failed

powershell -NoProfile -ExecutionPolicy Bypass ^
  -File "tools\prepare_ai_bundle.ps1" ^
  -BundleRoot "%OUTPUT_DIR%\_AIExplorer_Data\models\semantic" ^
  || goto :failed

powershell -NoProfile -ExecutionPolicy Bypass ^
  -Command "Compress-Archive -LiteralPath '%OUTPUT_DIR%' -DestinationPath '%OUTPUT_ZIP%' -CompressionLevel Optimal -Force" ^
  || goto :failed

echo.
echo Build completed:
echo %OUTPUT_ZIP%
echo The package already contains natural-language AI, semantic AI, visual AI, and CPU runtimes.
exit /b 0

:restore_failed
echo.
echo NuGet package restore failed.
echo Check access to https://api.nuget.org/v3/index.json
goto :failed

:failed
echo.
echo Release build failed.
pause
exit /b 1
