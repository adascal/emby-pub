# Kinopub Library Sync Guide

**Plugin**: Kinopub Emby Plugin  
**Feature**: Library Synchronization  
**Version**: 1.2.0  
**Last Updated**: 2025-01-23

## Overview

This guide explains how the Kinopub library synchronization feature works, how to configure it, and how to troubleshoot common issues.

## What is Library Sync?

Library Sync automatically synchronizes your Kinopub content (movies and TV shows) to your Emby library. Instead of browsing content through the Kinopub API in real-time, sync creates local .strm files that Emby can index and manage like any other media.

### Benefits

1. **Fast Browsing**: Content appears instantly (no API calls)
2. **Native Experience**: Full Emby library features (sorting, filtering, search)
3. **Offline Metadata**: Browse your library even when Kinopub API is slow
4. **Better Search**: Emby's powerful search works on your Kinopub content
5. **Collections**: Organize content with Emby collections and playlists

## How It Works

### Sync Process

```
1. Fetch Content from Kinopub API
   ├─ Bookmarks (your saved items)
   ├─ Collections (curated lists)
   └─ Continue Watching (in-progress items)

2. Process Each Item
   ├─ Movies: Generate .strm file
   └─ Series: Fetch episodes → Generate .strm file per episode

3. Emby Library Scan
   └─ Emby detects new .strm files → Fetches metadata → Updates library
```

### STRM Files

STRM files are simple text files containing a streaming URL. When you play a .strm file in Emby, it redirects to the actual stream.

**Example Movie STRM** (`kinopub-12345.strm`):
```
http://your-server:8096/Kinopub/Stream/12345?ApiKey={api_key}
```

**Example Episode STRM** (`kinopub-67890-e1.strm`):
```
http://your-server:8096/Kinopub/Stream/67890?EpisodeId=e1&ApiKey={api_key}
```

### Directory Structure

```
{LibraryPath}/
├── Movies/
│   ├── kinopub-12345.strm          ← Movie file
│   ├── kinopub-12346.strm
│   └── kinopub-12347.strm
└── TV Shows/
    ├── kinopub-67890/               ← Series folder
    │   ├── Season 01/               ← Season folder
    │   │   ├── kinopub-67890-e1.strm  ← Episode file
    │   │   ├── kinopub-67890-e2.strm
    │   │   └── kinopub-67890-e3.strm
    │   └── Season 02/
    │       ├── kinopub-67890-e4.strm
    │       └── kinopub-67890-e5.strm
    └── kinopub-67891/
        └── ...
```

## Configuration

### Initial Setup

1. **Install Plugin**
   - Download `Kinopub.Plugin.dll`
   - Place in Emby plugins folder: `~/.config/emby-server/plugins/`
   - Restart Emby Server

2. **Configure Plugin**
   - Navigate to Emby Dashboard → Plugins → Kinopub
   - Click "Configure"

3. **Authenticate with Kinopub**
   - Click "Get Device Code"
   - Visit the URL shown
   - Enter the device code
   - Authorize access
   - Wait for confirmation

4. **Configure Library Path**
   - Set "Library Path" to a writable directory
   - Example: `/mnt/media/kinopub-library`
   - Click "Save"

5. **Enable Library Sync**
   - Check "Enable Library Sync"
   - Select sync sources:
     - ☑ Sync Bookmarks
     - ☑ Sync Collections
     - ☑ Sync Continue Watching
   - Click "Save"

6. **Add Library to Emby**
   - Navigate to Emby Dashboard → Libraries
   - Click "+" to add new library
   - Select content type: "Movies" or "TV Shows"
   - Point to: `{LibraryPath}/Movies` or `{LibraryPath}/TV Shows`
   - Click "OK"

7. **Run Initial Sync**
   - Navigate to Emby Dashboard → Scheduled Tasks
   - Find "Sync Kinopub Library"
   - Click "Run Now"
   - Wait for completion (progress shown in task)

8. **Scan Library**
   - After sync completes, scan your new Emby library
   - Content should appear with metadata

### Configuration Options

#### Basic Settings

| Setting | Description | Default |
|---------|-------------|---------|
| Library Path | Directory for .strm files | (required) |
| Server URL | Base URL of your Emby server | http://localhost:8096 |
| Enable Library Sync | Master switch for sync feature | false |

#### Sync Sources

| Source | Description | Recommended |
|--------|-------------|-------------|
| Sync Bookmarks | Your saved/favorite items | ✅ Yes |
| Sync Collections | Curated lists from Kinopub | Optional |
| Sync Continue Watching | Items you're currently watching | ✅ Yes |

#### Performance Settings

| Setting | Description | Default | Range |
|---------|-------------|---------|-------|
| Batch Size | Items per batch | 50 | 10-100 |
| Max Concurrent Operations | Parallel batches | 4 | 1-10 |
| Cache Expiration (Hot) | Hot tier TTL (minutes) | 60 | 5-180 |
| Cache Expiration (Warm) | Warm tier TTL (minutes) | 15 | 2-60 |
| Cache Expiration (Cold) | Cold tier TTL (minutes) | 5 | 1-30 |
| Max Cache Entries | Max cached items | 1000 | 100-5000 |
| Enable Incremental Sync | Skip recently synced items | true | - |
| Incremental Sync Threshold | Hours before re-sync | 24 | 1-168 |
| Enable Parallel Processing | Use parallel batches | true | - |

### Performance Tuning

#### For Large Libraries (1000+ items)
```
Batch Size: 100
Max Concurrent Operations: 8
Enable Incremental Sync: true
Enable Parallel Processing: true
```

#### For Slow Internet
```
Batch Size: 25
Max Concurrent Operations: 2
Cache Expiration (Hot): 120
Cache Expiration (Warm): 30
```

#### For Fast Servers
```
Batch Size: 100
Max Concurrent Operations: 10
Max Cache Entries: 5000
```

## Sync Schedule

### Default Schedule

The sync task runs **daily at 2:00 AM** by default.

### Custom Schedule

1. Navigate to Emby Dashboard → Scheduled Tasks
2. Find "Sync Kinopub Library"
3. Click task name
4. Configure triggers:
   - **Daily**: Specific time each day
   - **Weekly**: Specific day and time
   - **Interval**: Every N hours/minutes
   - **Manual**: Only when manually triggered

### Recommended Schedules

**Active Users** (daily new content):
- Daily at 2:00 AM
- Incremental sync enabled

**Casual Users** (occasional watching):
- Weekly on Sunday at 2:00 AM
- Full sync (incremental disabled)

**Power Users** (multiple syncs per day):
- Every 6 hours
- Incremental sync enabled
- High performance settings

## Sync Modes

### Full Sync

**When**: First time, or incremental sync disabled  
**Duration**: 10-30 minutes for 1000 items  
**API Calls**: High (~1500 calls)  
**Behavior**: Syncs all items, even if already synced

**Use Case**:
- Initial setup
- After Kinopub library changes significantly
- After clearing Emby library

### Incremental Sync

**When**: Incremental sync enabled (default)  
**Duration**: 30 seconds - 2 minutes for 1000 items  
**API Calls**: Low (~50-150 calls)  
**Behavior**: Skips items synced within threshold (24h default)

**Use Case**:
- Daily updates
- Maintenance syncs
- Regular usage

### Comparison

| Aspect | Full Sync | Incremental Sync |
|--------|-----------|------------------|
| Speed | Slow (25 min) | Fast (30 sec) |
| API Calls | High (1500) | Low (150) |
| Thoroughness | Complete | Updates only |
| Server Load | High | Low |
| Recommended | Monthly | Daily |

## Monitoring Sync

### Progress Tracking

While sync is running, you can monitor progress:

1. Navigate to Emby Dashboard → Scheduled Tasks
2. Click "Sync Kinopub Library"
3. View real-time progress:
   - Percentage complete
   - Items processed / total
   - Current action
   - Estimated time remaining

### Sync Statistics

After sync completes, review statistics:

```
Sync Result:
- Total Items: 1000
- Processed: 1000
- Successful: 995
- Failed: 5
- Skipped: 0
- Movies Created: 650
- Series Created: 50
- Episodes Created: 8,500
- Duration: 6 minutes 23 seconds
- Cache Hit Rate: 87%
```

### Log Files

Detailed logs available in Emby logs folder:

```bash
~/.config/emby-server/logs/
```

Search for "Kinopub" to find relevant log entries:

```bash
grep -i "kinopub" ~/.config/emby-server/logs/embyserver.txt
```

**Common Log Patterns**:
- `[Kinopub] Starting library sync...` - Sync started
- `[Kinopub] Processed item: {title}` - Item processed
- `[Kinopub] Sync complete: {stats}` - Sync finished
- `[Kinopub] Error: {message}` - Error occurred

## Troubleshooting

### Sync Doesn't Start

**Symptoms**: Task shows as running but no progress

**Causes**:
1. Library sync not enabled
2. No valid authentication
3. Library path not set

**Solutions**:
1. Check "Enable Library Sync" is checked
2. Re-authenticate with Kinopub
3. Verify library path exists and is writable

### No Files Created

**Symptoms**: Sync completes but no .strm files in directory

**Causes**:
1. Wrong library path
2. Permission issues
3. No items in sync sources

**Solutions**:
```bash
# Check directory exists
ls -la {LibraryPath}

# Check permissions
chmod 755 {LibraryPath}
chown emby:emby {LibraryPath}

# Verify sync sources
# - Check at least one source is enabled
# - Verify you have bookmarks/watching items in Kinopub
```

### Files Created But Not in Emby

**Symptoms**: .strm files exist but don't appear in Emby library

**Causes**:
1. Library not added to Emby
2. Library not scanned after sync
3. Wrong content type for library

**Solutions**:
1. Add library in Emby Dashboard → Libraries
2. Run library scan after sync completes
3. Ensure Movies library points to Movies/ folder, TV library to TV Shows/

### Sync Very Slow

**Symptoms**: Sync takes hours instead of minutes

**Causes**:
1. Low batch size
2. Low concurrency
3. Slow internet
4. Incremental sync disabled

**Solutions**:
```
Increase performance settings:
- Batch Size: 50 → 100
- Max Concurrent Operations: 4 → 8
- Enable Incremental Sync: true
```

### Frequent Authentication Failures

**Symptoms**: Sync fails with "Unauthorized" errors

**Causes**:
1. Token expired
2. Invalid refresh token
3. Kinopub API issues

**Solutions**:
1. Re-authenticate through configuration page
2. Check Kinopub service status
3. Verify API credentials are correct

### Some Items Fail to Sync

**Symptoms**: Sync completes but some items show errors

**Causes**:
1. Invalid item data from API
2. Missing episodes for series
3. Network timeouts

**Solutions**:
1. Check logs for specific error messages
2. Run sync again (may be transient)
3. Report persistent failures as bug

### Files Play But Have No Metadata

**Symptoms**: .strm files play but show no title/poster

**Causes**:
1. Metadata providers not enabled
2. Kinopub provider ID missing
3. API rate limiting

**Solutions**:
1. Enable Kinopub metadata provider in library settings
2. Run "Refresh Metadata" on library
3. Wait a few minutes and try again

## Advanced Usage

### Manual Sync via API

Trigger sync programmatically:

```bash
curl -X POST "http://localhost:8096/ScheduledTasks/Running/{TaskId}" \
  -H "X-Emby-Token: {YourApiKey}"
```

### Sync Specific Categories

Modify configuration to sync only specific sources:

```csharp
// Only bookmarks
SyncBookmarks = true
SyncCollections = false
SyncContinueWatching = false
```

### Custom Library Structure

Advanced users can customize structure by modifying LibraryManager.cs:

```csharp
// Example: Organize by genre
public string GetMovieFilePath(string itemId, string genre)
{
    return Path.Combine(_config.LibraryPath, "Movies", genre, $"kinopub-{itemId}.strm");
}
```

### Sync State Management

View sync state file:

```bash
cat {LibraryPath}/.kinopub-sync-state.json | jq
```

Clear sync state (force full re-sync):

```bash
rm {LibraryPath}/.kinopub-sync-state.json
```

Backup sync state:

```bash
cp {LibraryPath}/.kinopub-sync-state.json \
   {LibraryPath}/.kinopub-sync-state.json.backup
```

### Performance Monitoring

Monitor cache performance:

```csharp
var stats = _cache.GetStatistics();
Console.WriteLine($"Hit Rate: {stats.HitRate:P2}");
Console.WriteLine($"Total Entries: {stats.TotalEntries}");
Console.WriteLine($"Hot: {stats.HotEntries}, Warm: {stats.WarmEntries}, Cold: {stats.ColdEntries}");
```

## Best Practices

### 1. Initial Setup
- Start with default settings
- Run full sync overnight
- Monitor first sync completion
- Adjust settings if needed

### 2. Regular Maintenance
- Use incremental sync for daily updates
- Full sync monthly
- Monitor sync statistics
- Review logs for errors

### 3. Performance
- Enable incremental sync
- Use parallel processing
- Tune batch size for your system
- Monitor API rate limits

### 4. Storage
- Ensure sufficient disk space (1 KB per file)
- Use SSD for library path (faster)
- Keep separate from main media storage
- Regular backups of sync state

### 5. Troubleshooting
- Check logs first
- Verify permissions
- Test with small library first
- Report bugs with logs

## FAQ

### Q: How much disk space do I need?
**A**: Very little. Each .strm file is ~150 bytes. 1000 items = ~150 KB.

### Q: Do .strm files contain actual video?
**A**: No, they only contain URLs. Video streams from Kinopub when played.

### Q: Can I sync multiple Kinopub accounts?
**A**: Currently no. One account per Emby server.

### Q: Will sync affect my Kinopub account?
**A**: No, sync only reads data. It doesn't modify your Kinopub library.

### Q: Can I manually add/remove items?
**A**: Yes, but next sync may recreate deleted files. Use .kinopub-ignore file to exclude items.

### Q: Does sync work offline?
**A**: No, requires internet for Kinopub API access. But browsing synced content works offline.

### Q: How often should I sync?
**A**: Daily is recommended. Incremental sync is very fast.

### Q: Can I sync to network storage?
**A**: Yes, but ensure good network speed and proper permissions.

### Q: What happens if sync is interrupted?
**A**: Next sync resumes. State file tracks progress.

### Q: Can I have multiple libraries?
**A**: Yes, one for Movies and one for TV Shows is standard.

## Support

### Getting Help

1. **Check Documentation**: This guide + IMPLEMENTATION_PROGRESS.md
2. **Review Logs**: `~/.config/emby-server/logs/`
3. **GitHub Issues**: Report bugs with logs
4. **Emby Forums**: Community support

### Reporting Bugs

Include:
1. Plugin version
2. Emby version
3. Error message
4. Relevant logs (sanitize tokens!)
5. Steps to reproduce

### Feature Requests

Submit via GitHub Issues with:
1. Use case description
2. Expected behavior
3. Why current features don't work

---

**Last Updated**: 2025-01-23  
**Plugin Version**: 1.2.0  
**Emby Compatibility**: 4.7.0.9+

For technical implementation details, see IMPLEMENTATION_PROGRESS.md and REFACTOR_PLAN.md.
