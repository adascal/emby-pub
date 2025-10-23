@echo off

REM Build script for Kinopub Emby Plugin (Windows)

echo Building Kinopub Plugin for Emby...

REM Clean previous builds
dotnet clean Kinopub.Plugin.csproj

REM Build the plugin
dotnet build Kinopub.Plugin.csproj -c Release

if %ERRORLEVEL% EQU 0 (
    echo.
    echo Build successful!
    echo Plugin DLL location: bin\Release\net6.0\Kinopub.Plugin.dll
    echo.
    echo To install:
    echo 1. Copy bin\Release\net6.0\Kinopub.Plugin.dll to your Emby plugins directory
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
