#!/bin/bash

# Build script for ServiceKP Emby Plugin

echo "Building ServiceKP Plugin for Emby..."

# Clean previous builds
dotnet clean ServiceKP.Plugin.csproj

# Build the plugin
dotnet build ServiceKP.Plugin.csproj -c Release

if [ $? -eq 0 ]; then
    echo ""
    echo "Build successful!"
    echo "Plugin DLL location: bin/Release/net6.0/ServiceKP.Plugin.dll"
    echo ""
    echo "To install:"
    echo "1. Copy bin/Release/net6.0/ServiceKP.Plugin.dll to your Emby plugins directory"
    echo "2. Restart Emby Server"
    echo ""
    echo "Plugin directories by platform:"
    echo "  Windows: C:\\Users\\[Username]\\AppData\\Roaming\\Emby-Server\\plugins"
    echo "  Linux:   /var/lib/emby/plugins"
    echo "  macOS:   ~/Library/Application Support/Emby-Server/plugins"
else
    echo ""
    echo "Build failed!"
    exit 1
fi
