# ServiceKP Plugin for Emby

A plugin for Emby Media Server that provides access to movies, TV shows, concerts, and documentaries from ServiceKP.

## Features

- Browse content by type (Movies, TV Shows, Concerts, Documentaries)
- Browse fresh, hot, and popular content
- Search functionality
- Personal bookmarks integration
- Multiple streaming quality options (HTTP, HLS, HLS2, HLS4)
- Multi-audio and subtitle support
- OAuth2 device flow authentication

## Installation

1. Build the plugin:
   ```bash
   dotnet build ServiceKP.Plugin.csproj -c Release
   ```

2. Copy the compiled DLL to your Emby plugins directory:
   - Windows: `C:\Users\[Username]\AppData\Roaming\Emby-Server\plugins`
   - Linux: `/var/lib/emby/plugins`
   - macOS: `~/Library/Application Support/Emby-Server/plugins`

3. Restart your Emby server

## Configuration

1. Navigate to **Emby Dashboard** > **Plugins** > **ServiceKP**

2. Configure the following settings:
   - **API Base URL**: Default is `https://api.service-kp.com`
   - **Client ID**: Your API client ID (default: `xbmc`)
   - **Client Secret**: Your API client secret
   - **Preferred Streaming Type**: Choose between HTTP, HLS, HLS2, or HLS4
   - **Items Per Page**: Number of items to load per page (10-100)
   - **Enable Adult Content**: Toggle adult content visibility

3. Click **Authenticate** to link your ServiceKP account:
   - Check your Emby server logs for the device code
   - Visit the verification URL shown in the logs
   - Enter the device code to authorize

4. Click **Save** to apply changes

## Usage

Once installed and configured:

1. Navigate to **Channels** in your Emby interface
2. Select **ServiceKP** channel
3. Browse content by:
   - **Browse by Type**: Movies, TV Shows, Concerts, etc.
   - **My Bookmarks**: Your saved content from ServiceKP
   - **Search**: Search for specific titles

## Content Types

- **Movies** - Feature films
- **TV Shows/Serials** - Television series
- **Concerts** - Music concerts and performances
- **Documentary Movies** - Documentary films
- **Documentary Serials** - Documentary series
- **TV Shows** - TV entertainment shows
- **3D Movies** - 3D content

## API Integration

This plugin integrates with the ServiceKP API, providing:

- Device authentication via OAuth2
- Content browsing and filtering
- Media streaming with multiple quality options
- Subtitle and audio track selection
- User bookmarks and watch history
- Search functionality

## Technical Details

### Requirements

- Emby Server 4.7.0 or higher
- .NET 6.0 or higher

### Project Structure

```
ServiceKP.Plugin/
├── Api/
│   └── ServiceKPApiClient.cs      # API client implementation
├── Channel/
│   └── ServiceKPChannel.cs        # Channel implementation
├── Configuration/
│   ├── configPage.html           # Configuration UI
│   └── PluginConfiguration.cs    # Configuration model
├── Models/
│   └── ApiModels.cs              # API response models
├── Plugin.cs                      # Main plugin entry point
└── ServiceKP.Plugin.csproj       # Project file
```

### Authentication Flow

1. Request device code from API
2. Display user code and verification URL in logs
3. User visits URL and enters code
4. Plugin polls API for authorization
5. Upon success, tokens are saved to configuration
6. Tokens are automatically refreshed when needed

## Building from Source

```bash
# Clone the repository
git clone <repository-url>
cd emby-pub

# Build the plugin
dotnet build ServiceKP.Plugin.csproj -c Release

# Output will be in bin/Release/net6.0/
```

## Troubleshooting

### Authentication Issues

- Check Emby server logs for device code and verification URL
- Ensure your API credentials are correct
- Verify network connectivity to `api.service-kp.com`

### Streaming Issues

- Try different streaming types (HTTP, HLS, HLS2, HLS4)
- Check your network bandwidth
- Verify Emby can access external URLs

### No Content Showing

- Ensure you're authenticated (check plugin configuration)
- Check server logs for API errors
- Verify adult content settings if applicable

## License

This plugin is provided as-is for use with Emby Media Server.

## Support

For issues and feature requests, please check the Emby server logs and ensure:
- You're using the latest version of the plugin
- Your API credentials are valid
- Your Emby server has internet access

## Credits

Developed for Emby Media Server to provide ServiceKP content integration.
