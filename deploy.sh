#!/bin/bash
# Deploy script for Kinopub Emby Plugin

set -e

echo "Deploying Kinopub Plugin to Emby..."

# Build the plugin
echo "Building plugin..."
dotnet build Kinopub.Plugin.csproj -c Release

# Define target directory
EMBY_PLUGINS_DIR="$HOME/.config/emby-server/plugins"

# Create plugins directory if it doesn't exist
mkdir -p "$EMBY_PLUGINS_DIR"

# Copy the DLL
echo "Copying plugin to Emby plugins directory..."
cp bin/Release/net6.0/Kinopub.Plugin.dll "$EMBY_PLUGINS_DIR/"

echo ""
echo "✓ Deployment successful!"
echo "Plugin installed to: $EMBY_PLUGINS_DIR/Kinopub.Plugin.dll"
echo ""
echo "Next steps:"
echo "1. Restart Emby Server"
echo "2. Go to Dashboard → Plugins to configure Kinopub"
echo ""
echo "To restart Emby:"
echo "  systemctl restart emby-server  (Linux with systemd)"
echo "  brew services restart emby-server  (macOS with Homebrew)"
