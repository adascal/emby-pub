#!/bin/bash

# Build script for Kinopub Emby Plugin

echo "Building Kinopub Plugin for Emby..."

# Clean previous builds
dotnet clean Kinopub.Plugin.csproj

# Build the plugin
dotnet build Kinopub.Plugin.csproj -c Release

if [ $? -eq 0 ]; then
    echo ""
    echo "Build successful!"
    echo "Plugin DLL location: bin/Release/net6.0/Kinopub.Plugin.dll"
    echo ""
    echo "To install:"
    echo "1. Copy bin/Release/net6.0/Kinopub.Plugin.dll to your Emby plugins directory"
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
