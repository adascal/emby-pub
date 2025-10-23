# Kinopub Plugin API Documentation

**Plugin**: Kinopub Emby Plugin  
**Version**: 1.2.0  
**Last Updated**: 2025-01-23

## Overview

This document describes the public APIs provided by the Kinopub plugin for Emby Server. These APIs enable streaming, authentication, and library management for Kinopub content.

## Base URL

All plugin endpoints are relative to your Emby server base URL:

```
http://{server}:{port}/Kinopub/...
```

Example:
```
http://localhost:8096/Kinopub/...
```

## Authentication

Most endpoints require Emby API key authentication:

**Header**:
```
X-Emby-Token: {your_api_key}
```

**Query Parameter** (alternative):
```
?ApiKey={your_api_key}
```

## Endpoints

### 1. Streaming

#### Get Stream URL
Returns a redirect to the actual Kinopub stream.

**Endpoint**: `GET /Kinopub/Stream/{itemId}`

**Parameters**:
| Name | Type | Required | Description |
|------|------|----------|-------------|
| itemId | string | Yes | Kinopub item ID |
| EpisodeId | string | No | Episode ID for series |
| ApiKey | string | Yes | Emby API key |

**Example - Movie**:
```http
GET /Kinopub/Stream/12345?ApiKey=abc123
```

**Example - Episode**:
```http
GET /Kinopub/Stream/67890?EpisodeId=e1&ApiKey=abc123
```

**Response**:
```http
HTTP/1.1 302 Found
Location: https://stream.kinopub.me/...
```

**Errors**:
```json
{
  "error": "Item not found",
  "code": 404
}
```

### 2. Authentication

#### Request Device Code
Initiates OAuth2 device flow.

**Endpoint**: `POST /Kinopub/Auth/DeviceCode`

**Request**:
```json
{
  "client_id": "xbmc",
  "client_secret": "cgg3gtifu46urtfp2zp1nqtba0k2ezxh"
}
```

**Response**:
```json
{
  "code": "ABCD1234",
  "user_code": "ABCD-1234",
  "verification_url": "https://kinopub.me/device",
  "expires_in": 600,
  "interval": 5
}
```

**Usage**:
1. Display `user_code` and `verification_url` to user
2. Poll token endpoint every `interval` seconds
3. Continue until token received or `expires_in` reached

#### Poll for Token
Polls for OAuth2 token after user authorization.

**Endpoint**: `POST /Kinopub/Auth/Token`

**Request**:
```json
{
  "code": "ABCD1234",
  "client_id": "xbmc",
  "client_secret": "cgg3gtifu46urtfp2zp1nqtba0k2ezxh"
}
```

**Response** (pending):
```json
{
  "error": "authorization_pending",
  "message": "User has not authorized yet"
}
```

**Response** (success):
```json
{
  "access_token": "eyJhbGc...",
  "refresh_token": "eyJhbGc...",
  "expires_in": 3600,
  "token_type": "Bearer"
}
```

#### Refresh Token
Refreshes an expired access token.

**Endpoint**: `POST /Kinopub/Auth/Refresh`

**Request**:
```json
{
  "refresh_token": "eyJhbGc...",
  "client_id": "xbmc",
  "client_secret": "cgg3gtifu46urtfp2zp1nqtba0k2ezxh"
}
```

**Response**:
```json
{
  "access_token": "eyJhbGc...",
  "refresh_token": "eyJhbGc...",
  "expires_in": 3600
}
```

### 3. Library Management

#### Trigger Sync
Manually triggers library sync task.

**Endpoint**: `POST /ScheduledTasks/Running/{taskId}`

**Headers**:
```
X-Emby-Token: {api_key}
```

**Response**:
```http
HTTP/1.1 204 No Content
```

**Find Task ID**:
```http
GET /ScheduledTasks
```

Look for task with `Name: "Sync Kinopub Library"`

#### Get Sync Status
Returns current sync status.

**Endpoint**: `GET /ScheduledTasks/Running`

**Response**:
```json
[
  {
    "Name": "Sync Kinopub Library",
    "Id": "abc-123",
    "State": "Running",
    "CurrentProgressPercentage": 45.5,
    "EstimatedCompletionTime": "2025-01-23T10:35:00Z"
  }
]
```

### 4. Configuration

#### Get Configuration
Returns current plugin configuration.

**Endpoint**: `GET /Plugins/Kinopub/Configuration`

**Headers**:
```
X-Emby-Token: {api_key}
```

**Response**:
```json
{
  "LibraryPath": "/mnt/media/kinopub",
  "ServerUrl": "http://localhost:8096",
  "EnableLibrarySync": true,
  "SyncBookmarks": true,
  "SyncCollections": true,
  "SyncContinueWatching": true,
  "BatchSize": 50,
  "MaxConcurrentOperations": 4,
  "EnableIncrementalSync": true,
  "IncrementalSyncThresholdHours": 24
}
```

#### Update Configuration
Updates plugin configuration.

**Endpoint**: `POST /Plugins/Kinopub/Configuration`

**Headers**:
```
X-Emby-Token: {api_key}
Content-Type: application/json
```

**Request**:
```json
{
  "LibraryPath": "/mnt/media/kinopub",
  "EnableLibrarySync": true,
  "BatchSize": 100
}
```

**Response**:
```http
HTTP/1.1 204 No Content
```

## Kinopub API Integration

The plugin internally uses Kinopub's REST API. These are not directly exposed but are documented here for reference.

### Base URL
```
https://api.service-kp.com
```

### Endpoints Used

#### Get User Bookmarks
```http
GET /v1/bookmarks
Authorization: Bearer {access_token}
```

**Response**:
```json
{
  "items": [
    {
      "id": 12345,
      "type": "movie",
      "title": "Movie Title",
      "year": 2023
    }
  ],
  "total": 100,
  "pagination": {
    "page": 1,
    "perpage": 20
  }
}
```

#### Get Item Details
```http
GET /v1/items/{id}/media
Authorization: Bearer {access_token}
```

**Response**:
```json
{
  "item": {
    "id": 12345,
    "type": "movie",
    "title": "Movie Title",
    "plot": "Description...",
    "year": 2023,
    "posters": [{"url": "https://..."}],
    "videos": [
      {
        "quality": "1080p",
        "url": "https://stream.kinopub.me/..."
      }
    ]
  }
}
```

#### Get Series Episodes
```http
GET /v1/items/{id}/media
Authorization: Bearer {access_token}
```

**Response**:
```json
{
  "item": {
    "id": 67890,
    "type": "serial",
    "title": "Series Title",
    "seasons": [
      {
        "number": 1,
        "episodes": [
          {
            "id": "e1",
            "number": 1,
            "title": "Episode 1",
            "videos": [{"url": "https://..."}]
          }
        ]
      }
    ]
  }
}
```

#### Get Continue Watching
```http
GET /v1/watching/serials
Authorization: Bearer {access_token}
```

**Response**:
```json
{
  "items": [
    {
      "id": 67890,
      "title": "Series Title",
      "watching": {
        "season": 2,
        "episode": 5,
        "time": 1234
      }
    }
  ]
}
```

## Error Codes

| Code | Description | Solution |
|------|-------------|----------|
| 400 | Bad Request | Check request parameters |
| 401 | Unauthorized | Refresh access token |
| 403 | Forbidden | Check API permissions |
| 404 | Not Found | Item doesn't exist |
| 429 | Rate Limited | Wait and retry |
| 500 | Server Error | Check Kinopub API status |

## Rate Limiting

### Kinopub API Limits
- **Requests per minute**: 60
- **Requests per hour**: 1000

### Plugin Caching
The plugin implements intelligent caching to minimize API calls:

- **Hot tier** (60min): Watching list, recent items
- **Warm tier** (15min): Collections, bookmarks
- **Cold tier** (5min): Search results

Expected cache hit rate: **85-90%**

### Best Practices
1. Use incremental sync for regular updates
2. Don't disable caching
3. Tune batch size and concurrency for your API limits
4. Monitor sync statistics for API call counts

## Webhooks

The plugin currently **does not support** webhooks. Real-time sync via webhooks is planned for v2.0.

## SDK/Client Libraries

### C# Example

```csharp
using System.Net.Http;
using System.Text.Json;

var client = new HttpClient();
client.BaseAddress = new Uri("http://localhost:8096");
client.DefaultRequestHeaders.Add("X-Emby-Token", "your_api_key");

// Trigger sync
var response = await client.PostAsync("/ScheduledTasks/Running/task-id", null);

// Get configuration
var configJson = await client.GetStringAsync("/Plugins/Kinopub/Configuration");
var config = JsonSerializer.Deserialize<PluginConfiguration>(configJson);
```

### Python Example

```python
import requests

base_url = "http://localhost:8096"
api_key = "your_api_key"

headers = {"X-Emby-Token": api_key}

# Get stream URL
response = requests.get(
    f"{base_url}/Kinopub/Stream/12345",
    params={"ApiKey": api_key},
    allow_redirects=False
)
stream_url = response.headers["Location"]

# Get configuration
response = requests.get(
    f"{base_url}/Plugins/Kinopub/Configuration",
    headers=headers
)
config = response.json()
```

### JavaScript Example

```javascript
const baseUrl = 'http://localhost:8096';
const apiKey = 'your_api_key';

// Trigger sync
fetch(`${baseUrl}/ScheduledTasks/Running/task-id`, {
  method: 'POST',
  headers: {
    'X-Emby-Token': apiKey
  }
});

// Get stream URL
fetch(`${baseUrl}/Kinopub/Stream/12345?ApiKey=${apiKey}`, {
  redirect: 'manual'
}).then(response => {
  const streamUrl = response.headers.get('Location');
  console.log('Stream URL:', streamUrl);
});
```

## Integration Examples

### Home Assistant

```yaml
# configuration.yaml
rest_command:
  kinopub_sync:
    url: "http://emby:8096/ScheduledTasks/Running/task-id"
    method: POST
    headers:
      X-Emby-Token: "your_api_key"

# automation
automation:
  - alias: "Sync Kinopub Daily"
    trigger:
      platform: time
      at: "02:00:00"
    action:
      service: rest_command.kinopub_sync
```

### Node-RED

```json
[
  {
    "type": "http request",
    "method": "POST",
    "url": "http://emby:8096/ScheduledTasks/Running/task-id",
    "headers": {
      "X-Emby-Token": "your_api_key"
    }
  }
]
```

### Cron Job

```bash
#!/bin/bash
# sync-kinopub.sh

API_KEY="your_api_key"
TASK_ID="task-id"
EMBY_URL="http://localhost:8096"

curl -X POST \
  -H "X-Emby-Token: $API_KEY" \
  "$EMBY_URL/ScheduledTasks/Running/$TASK_ID"
```

```cron
# Run daily at 2 AM
0 2 * * * /usr/local/bin/sync-kinopub.sh
```

## Testing

### Test Endpoints

Use curl to test endpoints:

```bash
# Test streaming endpoint
curl -I "http://localhost:8096/Kinopub/Stream/12345?ApiKey=your_key"

# Should return 302 redirect

# Test configuration
curl -H "X-Emby-Token: your_key" \
  "http://localhost:8096/Plugins/Kinopub/Configuration"

# Should return JSON configuration

# Trigger sync
curl -X POST \
  -H "X-Emby-Token: your_key" \
  "http://localhost:8096/ScheduledTasks/Running/task-id"

# Should return 204 No Content
```

### Postman Collection

Import this collection for easy API testing:

```json
{
  "info": {
    "name": "Kinopub Plugin API",
    "schema": "https://schema.getpostman.com/json/collection/v2.1.0/collection.json"
  },
  "variable": [
    {
      "key": "base_url",
      "value": "http://localhost:8096"
    },
    {
      "key": "api_key",
      "value": "your_api_key"
    }
  ],
  "item": [
    {
      "name": "Get Stream",
      "request": {
        "method": "GET",
        "url": "{{base_url}}/Kinopub/Stream/12345?ApiKey={{api_key}}"
      }
    },
    {
      "name": "Get Configuration",
      "request": {
        "method": "GET",
        "url": "{{base_url}}/Plugins/Kinopub/Configuration",
        "header": [
          {
            "key": "X-Emby-Token",
            "value": "{{api_key}}"
          }
        ]
      }
    }
  ]
}
```

## Security Considerations

### API Key Protection
- Never commit API keys to version control
- Use environment variables for keys
- Rotate keys periodically
- Restrict key permissions to minimum required

### HTTPS
- Use HTTPS in production
- Enable SSL/TLS on Emby server
- Validate certificates

### Access Control
- Limit API access to trusted IPs
- Use Emby's built-in authentication
- Don't expose Emby to public internet without protection

## Versioning

The plugin follows semantic versioning:

**Format**: `MAJOR.MINOR.PATCH`

- **MAJOR**: Breaking API changes
- **MINOR**: New features, backward compatible
- **PATCH**: Bug fixes

**Current Version**: 1.2.0

### API Compatibility

| Plugin Version | Emby Version | Kinopub API |
|----------------|--------------|-------------|
| 1.0.x | 4.7.0+ | v1 |
| 1.1.x | 4.7.0+ | v1 |
| 1.2.x | 4.7.0+ | v1 |

## Changelog

### v1.2.0 (2025-01-23)
- Added comprehensive test suite
- Improved error handling
- Enhanced documentation

### v1.1.0
- Added performance optimizations
- Multi-tier caching
- Incremental sync
- Parallel processing

### v1.0.0
- Initial library-based implementation
- Basic sync functionality
- Metadata providers

## Support

### Documentation
- IMPLEMENTATION_PROGRESS.md - Technical details
- REFACTOR_PLAN.md - Architecture decisions
- LIBRARY_SYNC.md - User guide

### Community
- GitHub Issues: Bug reports and features
- Emby Forums: Community support
- Documentation Wiki: Additional guides

### Commercial Support
Currently not available. Plugin is open-source and community-supported.

---

**Last Updated**: 2025-01-23  
**Plugin Version**: 1.2.0  
**API Version**: 1.0  
**Emby Compatibility**: 4.7.0.9+
