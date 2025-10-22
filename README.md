# ServiceKP Plugin for Emby

A plugin for Emby Media Server that provides access to movies, TV shows, concerts, and documentaries from ServiceKP.

## Features

- **Content Browsing**
  - Browse by type (Movies, TV Shows, Concerts, Documentaries, 3D Movies)
  - Fresh, hot, and popular content categories with customizable sorting
  - "All" category with full sorting options (rating, date, title)
  - Full season and episode navigation for TV shows
  - Collections/curated playlists
  - Live TV channels (sports events, special broadcasts)
  - Trailers for movies and shows
  - "Similar Items" recommendations for content discovery

- **Advanced Filtering**
  - Filter by genre (contextual to content type)
  - Filter by country
  - Filter by year (coming soon)
  - Multiple filter combinations

- **User Features**
  - Continue Watching - Resume shows with new episodes
  - Watch History - Track your viewing history
  - Personal Bookmarks - Access your saved content
  - Search functionality
  - Watch status tracking (API integrated)

- **Streaming Features**
  - Multiple streaming protocols (HTTP, HLS, HLS2, HLS4)
  - Multi-audio track support with language selection
  - Subtitle support (embedded and external)
  - Multiple video quality options
  - Resume playback from last position
  - Video stream metadata (resolution, codec)

- **Performance**
  - Smart caching of reference data (types, genres, countries)
  - Reduced API calls for improved responsiveness
  - Configurable cache TTL

- **Authentication**
  - OAuth2 device flow authentication
  - Automatic token refresh
  - Secure credential management

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
   - **Browse by Type**: Movies, TV Shows, Concerts, Documentaries, etc.
     - **All**: Browse all content with sorting options
     - **Fresh**: Newly added content
     - **Hot**: Trending/popular content
     - **Popular**: All-time popular content
     - **Filters**: Advanced filtering by genre and country
   - **Continue Watching**: Shows with new episodes you're following
   - **Collections**: Curated playlists and collections
   - **My Bookmarks**: Your saved content from ServiceKP
   - **Live TV**: Active live channels (sports events, special broadcasts)
   - **Watch History**: Your recent viewing history
   - **Search**: Search for specific titles

4. Advanced Filtering:
   - Select **Filters** under any content type
   - Choose **Browse by Genre** to filter by genre
   - Choose **Browse by Country** to filter by country
   - Genres are contextual (different for movies vs. documentaries)

5. For Movies and Shows:
   - View **Trailer** before watching (if available)
   - Browse **Similar Items** for recommendations
   - For TV Shows:
     - Click on a show to see seasons
     - Click on a season to see episodes
     - Episodes automatically show your watch progress
     - Resume watching from where you left off

6. Video Playback:
   - Select audio tracks during playback (if multiple available)
   - Enable/disable subtitles (if available)
   - Video quality is automatically selected based on your settings
   - Playback position is tracked automatically

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
