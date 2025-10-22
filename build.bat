@echo off

REM Build script for ServiceKP Emby Plugin (Windows)

echo Building ServiceKP Plugin for Emby...

REM Clean previous builds
dotnet clean ServiceKP.Plugin.csproj

REM Build the plugin
dotnet build ServiceKP.Plugin.csproj -c Release

if %ERRORLEVEL% EQU 0 (
    echo.
    echo Build successful!
    echo Plugin DLL location: bin\Release\net6.0\ServiceKP.Plugin.dll
    echo.
    echo To install:
    echo 1. Copy bin\Release\net6.0\ServiceKP.Plugin.dll to your Emby plugins directory
    echo 2. Restart Emby Server
    echo.
    echo Default plugin directory:
    echo   C:\Users\%USERNAME%\AppData\Roaming\Emby-Server\plugins
) else (
    echo.
    echo Build failed!
    exit /b 1
)

pause
