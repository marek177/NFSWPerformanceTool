@echo off
setlocal
cd /d "%~dp0"

where dotnet >nul 2>&1
if errorlevel 1 (
    echo ERROR: .NET 8 SDK was not found. Install it from https://dotnet.microsoft.com/download/dotnet/8.0
    exit /b 1
)

powershell.exe -NoLogo -NoProfile -ExecutionPolicy Bypass -File "%~dp0build-release.ps1" -RuntimeIdentifiers win-x86 -OutputDirectory artifacts
set "BUILD_EXIT_CODE=%ERRORLEVEL%"

if not "%BUILD_EXIT_CODE%"=="0" (
    echo Build failed with exit code %BUILD_EXIT_CODE%.
    exit /b %BUILD_EXIT_CODE%
)

echo Build completed successfully. Output: "%~dp0artifacts"
exit /b 0
