using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using MediaBrowser.Model.Logging;

namespace Kinopub.Plugin.Library
{
    /// <summary>
    /// Manages persistent sync state for incremental synchronization
    /// </summary>
    public class SyncStateManager
    {
        private readonly ILogger _logger;
        private readonly string _stateFilePath;
        private readonly SemaphoreSlim _stateLock;
        private SyncState _state;

        private const string StateFileName = ".kinopub-sync-state.json";

        public SyncStateManager(ILogger logger, string libraryPath)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _stateFilePath = Path.Combine(libraryPath, StateFileName);
            _stateLock = new SemaphoreSlim(1, 1);
            _state = new SyncState();
        }

        /// <summary>
        /// Loads sync state from disk
        /// </summary>
        public async Task<bool> LoadStateAsync(CancellationToken cancellationToken = default)
        {
            await _stateLock.WaitAsync(cancellationToken);
            try
            {
                if (!File.Exists(_stateFilePath))
                {
                    _logger.Info("No sync state file found, starting fresh");
                    _state = new SyncState();
                    return false;
                }

                var json = await File.ReadAllTextAsync(_stateFilePath, cancellationToken);
                _state = JsonSerializer.Deserialize<SyncState>(json) ?? new SyncState();
                _logger.Info($"Loaded sync state: {_state.Items.Count} items tracked");
                return true;
            }
            catch (Exception ex)
            {
                _logger.Error($"Failed to load sync state: {ex.Message}");
                _state = new SyncState();
                return false;
            }
            finally
            {
                _stateLock.Release();
            }
        }

        /// <summary>
        /// Saves sync state to disk
        /// </summary>
        public async Task<bool> SaveStateAsync(CancellationToken cancellationToken = default)
        {
            await _stateLock.WaitAsync(cancellationToken);
            try
            {
                var json = JsonSerializer.Serialize(_state, new JsonSerializerOptions
                {
                    WriteIndented = true
                });

                var tempFile = _stateFilePath + ".tmp";
                await File.WriteAllTextAsync(tempFile, json, cancellationToken);
                File.Move(tempFile, _stateFilePath, overwrite: true);

                _logger.Info($"Saved sync state: {_state.Items.Count} items tracked");
                return true;
            }
            catch (Exception ex)
            {
                _logger.Error($"Failed to save sync state: {ex.Message}");
                return false;
            }
            finally
            {
                _stateLock.Release();
            }
        }

        /// <summary>
        /// Records an item as synced
        /// </summary>
        public async Task RecordItemAsync(string itemId, string filePath, string? metadataHash = null, CancellationToken cancellationToken = default)
        {
            await _stateLock.WaitAsync(cancellationToken);
            try
            {
                var itemState = new ItemSyncState
                {
                    ItemId = itemId,
                    LastSyncTime = DateTime.UtcNow,
                    FilePath = filePath,
                    MetadataHash = metadataHash
                };

                _state.Items[itemId] = itemState;
                _state.LastSyncTime = DateTime.UtcNow;
            }
            finally
            {
                _stateLock.Release();
            }
        }

        /// <summary>
        /// Records multiple items as synced
        /// </summary>
        public async Task RecordItemsAsync(IEnumerable<(string itemId, string filePath, string? metadataHash)> items, CancellationToken cancellationToken = default)
        {
            await _stateLock.WaitAsync(cancellationToken);
            try
            {
                foreach (var (itemId, filePath, metadataHash) in items)
                {
                    var itemState = new ItemSyncState
                    {
                        ItemId = itemId,
                        LastSyncTime = DateTime.UtcNow,
                        FilePath = filePath,
                        MetadataHash = metadataHash
                    };

                    _state.Items[itemId] = itemState;
                }

                _state.LastSyncTime = DateTime.UtcNow;
            }
            finally
            {
                _stateLock.Release();
            }
        }

        /// <summary>
        /// Checks if an item was synced after a given timestamp
        /// </summary>
        public bool WasItemSyncedAfter(string itemId, DateTime timestamp)
        {
            if (_state.Items.TryGetValue(itemId, out var itemState))
            {
                return itemState.LastSyncTime > timestamp;
            }
            return false;
        }

        /// <summary>
        /// Gets the last sync time for an item
        /// </summary>
        public DateTime? GetItemLastSyncTime(string itemId)
        {
            if (_state.Items.TryGetValue(itemId, out var itemState))
            {
                return itemState.LastSyncTime;
            }
            return null;
        }

        /// <summary>
        /// Gets all items synced after a given timestamp
        /// </summary>
        public IEnumerable<string> GetItemsSyncedAfter(DateTime timestamp)
        {
            return _state.Items
                .Where(kvp => kvp.Value.LastSyncTime > timestamp)
                .Select(kvp => kvp.Key);
        }

        /// <summary>
        /// Gets all items that need re-sync (based on metadata hash change or missing files)
        /// </summary>
        public async Task<IEnumerable<string>> GetItemsNeedingResyncAsync(CancellationToken cancellationToken = default)
        {
            await _stateLock.WaitAsync(cancellationToken);
            try
            {
                var needsResync = new List<string>();

                foreach (var kvp in _state.Items)
                {
                    var itemState = kvp.Value;

                    // Check if file still exists
                    if (!File.Exists(itemState.FilePath))
                    {
                        needsResync.Add(kvp.Key);
                        continue;
                    }

                    // Check if metadata hash changed (if provided)
                    if (!string.IsNullOrEmpty(itemState.MetadataHash))
                    {
                        // TODO: Implement metadata hash comparison
                        // For now, skip items with existing files
                    }
                }

                return needsResync;
            }
            finally
            {
                _stateLock.Release();
            }
        }

        /// <summary>
        /// Removes an item from sync state
        /// </summary>
        public async Task RemoveItemAsync(string itemId, CancellationToken cancellationToken = default)
        {
            await _stateLock.WaitAsync(cancellationToken);
            try
            {
                _state.Items.TryRemove(itemId, out _);
            }
            finally
            {
                _stateLock.Release();
            }
        }

        /// <summary>
        /// Removes multiple items from sync state
        /// </summary>
        public async Task RemoveItemsAsync(IEnumerable<string> itemIds, CancellationToken cancellationToken = default)
        {
            await _stateLock.WaitAsync(cancellationToken);
            try
            {
                foreach (var itemId in itemIds)
                {
                    _state.Items.TryRemove(itemId, out _);
                }
            }
            finally
            {
                _stateLock.Release();
            }
        }

        /// <summary>
        /// Clears all sync state
        /// </summary>
        public async Task ClearStateAsync(CancellationToken cancellationToken = default)
        {
            await _stateLock.WaitAsync(cancellationToken);
            try
            {
                _state = new SyncState();
                if (File.Exists(_stateFilePath))
                {
                    File.Delete(_stateFilePath);
                }
                _logger.Info("Cleared sync state");
            }
            finally
            {
                _stateLock.Release();
            }
        }

        /// <summary>
        /// Gets sync state statistics
        /// </summary>
        public SyncStateStatistics GetStatistics()
        {
            return new SyncStateStatistics
            {
                TotalItemsTracked = _state.Items.Count,
                LastSyncTime = _state.LastSyncTime,
                OldestItemSyncTime = _state.Items.Values.Any() 
                    ? _state.Items.Values.Min(i => i.LastSyncTime) 
                    : (DateTime?)null,
                NewestItemSyncTime = _state.Items.Values.Any() 
                    ? _state.Items.Values.Max(i => i.LastSyncTime) 
                    : (DateTime?)null
            };
        }

        /// <summary>
        /// Creates a backup of the current state
        /// </summary>
        public async Task<string?> CreateBackupAsync(CancellationToken cancellationToken = default)
        {
            await _stateLock.WaitAsync(cancellationToken);
            try
            {
                if (!File.Exists(_stateFilePath))
                {
                    return null;
                }

                var backupPath = $"{_stateFilePath}.backup.{DateTime.UtcNow:yyyyMMddHHmmss}";
                File.Copy(_stateFilePath, backupPath);
                _logger.Info($"Created state backup: {backupPath}");
                return backupPath;
            }
            catch (Exception ex)
            {
                _logger.Error($"Failed to create state backup: {ex.Message}");
                return null;
            }
            finally
            {
                _stateLock.Release();
            }
        }

        /// <summary>
        /// Restores state from a backup file
        /// </summary>
        public async Task<bool> RestoreFromBackupAsync(string backupPath, CancellationToken cancellationToken = default)
        {
            await _stateLock.WaitAsync(cancellationToken);
            try
            {
                if (!File.Exists(backupPath))
                {
                    _logger.Error($"Backup file not found: {backupPath}");
                    return false;
                }

                File.Copy(backupPath, _stateFilePath, overwrite: true);
                await LoadStateAsync(cancellationToken);
                _logger.Info($"Restored state from backup: {backupPath}");
                return true;
            }
            catch (Exception ex)
            {
                _logger.Error($"Failed to restore from backup: {ex.Message}");
                return false;
            }
            finally
            {
                _stateLock.Release();
            }
        }
    }

    /// <summary>
    /// Represents the entire sync state
    /// </summary>
    public class SyncState
    {
        public ConcurrentDictionary<string, ItemSyncState> Items { get; set; } = new();
        public DateTime? LastSyncTime { get; set; }
    }

    /// <summary>
    /// Represents sync state for a single item
    /// </summary>
    public class ItemSyncState
    {
        public string ItemId { get; set; } = string.Empty;
        public DateTime LastSyncTime { get; set; }
        public string FilePath { get; set; } = string.Empty;
        public string? MetadataHash { get; set; }
    }

    /// <summary>
    /// Statistics about sync state
    /// </summary>
    public class SyncStateStatistics
    {
        public int TotalItemsTracked { get; set; }
        public DateTime? LastSyncTime { get; set; }
        public DateTime? OldestItemSyncTime { get; set; }
        public DateTime? NewestItemSyncTime { get; set; }
    }
}
