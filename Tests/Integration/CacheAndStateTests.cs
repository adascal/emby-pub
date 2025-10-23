using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using FluentAssertions;
using Kinopub.Plugin.Api;
using Kinopub.Plugin.Library;
using Kinopub.Plugin.Tests.Helpers;
using Xunit;

namespace Kinopub.Plugin.Tests.Integration
{
    public class EnhancedCacheTests
    {
        [Fact]
        public void TryGet_WithNonExistentKey_ShouldReturnFalse()
        {
            // Arrange
            var logger = TestHelpers.CreateMockLogger();
            var cache = new EnhancedCache(logger);

            // Act
            var found = cache.TryGet<string>("nonexistent", out var value);

            // Assert
            found.Should().BeFalse();
            value.Should().BeNull();
        }

        [Fact]
        public void Set_AndGet_ShouldReturnValue()
        {
            // Arrange
            var logger = TestHelpers.CreateMockLogger();
            var cache = new EnhancedCache(logger);
            var key = "test_key";
            var expectedValue = "test_value";

            // Act
            cache.Set(key, expectedValue);
            var found = cache.TryGet<string>(key, out var actualValue);

            // Assert
            found.Should().BeTrue();
            actualValue.Should().Be(expectedValue);
        }

        [Fact]
        public void Set_WithHotTier_ShouldUseHotTTL()
        {
            // Arrange
            var logger = TestHelpers.CreateMockLogger();
            var cache = new EnhancedCache(logger);

            // Act
            cache.Set("hot_key", "hot_value", CacheTier.Hot);
            var stats = cache.GetStatistics();

            // Assert
            stats.TotalEntries.Should().Be(1);
            stats.HotEntries.Should().Be(1);
        }

        [Fact]
        public void Set_WithWarmTier_ShouldUseWarmTTL()
        {
            // Arrange
            var logger = TestHelpers.CreateMockLogger();
            var cache = new EnhancedCache(logger);

            // Act
            cache.Set("warm_key", "warm_value", CacheTier.Warm);
            var stats = cache.GetStatistics();

            // Assert
            stats.WarmEntries.Should().Be(1);
        }

        [Fact]
        public void Set_WithColdTier_ShouldUseColdTTL()
        {
            // Arrange
            var logger = TestHelpers.CreateMockLogger();
            var cache = new EnhancedCache(logger);

            // Act
            cache.Set("cold_key", "cold_value", CacheTier.Cold);
            var stats = cache.GetStatistics();

            // Assert
            stats.ColdEntries.Should().Be(1);
        }

        [Fact]
        public void GetStatistics_ShouldTrackHitRate()
        {
            // Arrange
            var logger = TestHelpers.CreateMockLogger();
            var cache = new EnhancedCache(logger);
            cache.Set("key1", "value1");

            // Act
            cache.TryGet<string>("key1", out _); // hit
            cache.TryGet<string>("key2", out _); // miss
            cache.TryGet<string>("key1", out _); // hit
            
            var stats = cache.GetStatistics();

            // Assert
            stats.HitCount.Should().Be(2);
            stats.MissCount.Should().Be(1);
            stats.HitRate.Should().BeApproximately(0.666, 0.01);
        }

        [Fact]
        public void Set_ExceedingMaxEntries_ShouldEvictOldest()
        {
            // Arrange
            var logger = TestHelpers.CreateMockLogger();
            var cache = new EnhancedCache(logger, maxEntries: 10);

            // Act - Add 12 entries (exceeds max)
            for (int i = 0; i < 12; i++)
            {
                cache.Set($"key{i}", $"value{i}");
            }

            var stats = cache.GetStatistics();

            // Assert - Should have evicted entries
            stats.TotalEntries.Should().BeLessThanOrEqualTo(10);
        }

        [Fact]
        public void Remove_ShouldRemoveEntry()
        {
            // Arrange
            var logger = TestHelpers.CreateMockLogger();
            var cache = new EnhancedCache(logger);
            cache.Set("key", "value");

            // Act
            cache.Remove("key");
            var found = cache.TryGet<string>("key", out _);

            // Assert
            found.Should().BeFalse();
        }

        [Fact]
        public void Clear_ShouldRemoveAllEntries()
        {
            // Arrange
            var logger = TestHelpers.CreateMockLogger();
            var cache = new EnhancedCache(logger);
            cache.Set("key1", "value1");
            cache.Set("key2", "value2");

            // Act
            cache.Clear();
            var stats = cache.GetStatistics();

            // Assert
            stats.TotalEntries.Should().Be(0);
        }
    }

    public class SyncStateManagerTests : IDisposable
    {
        private readonly string _tempPath;
        private readonly SyncStateManager _stateManager;

        public SyncStateManagerTests()
        {
            _tempPath = TestHelpers.CreateTempDirectory();
            var logger = TestHelpers.CreateMockLogger();
            _stateManager = new SyncStateManager(logger, _tempPath);
        }

        public void Dispose()
        {
            TestHelpers.CleanupTempDirectory(_tempPath);
        }

        [Fact]
        public async Task LoadStateAsync_WithNoFile_ShouldReturnFalse()
        {
            // Act
            var loaded = await _stateManager.LoadStateAsync();

            // Assert
            loaded.Should().BeFalse();
        }

        [Fact]
        public async Task RecordItemAsync_AndSaveState_ShouldPersist()
        {
            // Arrange
            var itemId = "12345";
            var filePath = "/path/to/file.strm";

            // Act
            await _stateManager.RecordItemAsync(itemId, filePath);
            await _stateManager.SaveStateAsync();
            
            // Reload from disk
            var newStateManager = new SyncStateManager(TestHelpers.CreateMockLogger(), _tempPath);
            var loaded = await newStateManager.LoadStateAsync();
            var lastSync = newStateManager.GetItemLastSyncTime(itemId);

            // Assert
            loaded.Should().BeTrue();
            lastSync.Should().NotBeNull();
            lastSync.Value.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(5));
        }

        [Fact]
        public async Task RecordItemsAsync_ShouldRecordMultipleItems()
        {
            // Arrange
            var items = new[]
            {
                ("item1", "/path/file1.strm", (string?)null),
                ("item2", "/path/file2.strm", (string?)null),
                ("item3", "/path/file3.strm", (string?)null)
            };

            // Act
            await _stateManager.RecordItemsAsync(items);
            await _stateManager.SaveStateAsync();

            var stats = _stateManager.GetStatistics();

            // Assert
            stats.TotalItemsTracked.Should().Be(3);
        }

        [Fact]
        public void WasItemSyncedAfter_WithRecentSync_ShouldReturnTrue()
        {
            // Arrange
            var itemId = "12345";
            var filePath = "/path/to/file.strm";
            var timestamp = DateTime.UtcNow.AddHours(-1);
            
            _stateManager.RecordItemAsync(itemId, filePath).Wait();

            // Act
            var wasSynced = _stateManager.WasItemSyncedAfter(itemId, timestamp);

            // Assert
            wasSynced.Should().BeTrue();
        }

        [Fact]
        public void WasItemSyncedAfter_WithOldSync_ShouldReturnFalse()
        {
            // Arrange
            var itemId = "12345";
            var filePath = "/path/to/file.strm";
            var timestamp = DateTime.UtcNow.AddHours(1); // Future timestamp
            
            _stateManager.RecordItemAsync(itemId, filePath).Wait();

            // Act
            var wasSynced = _stateManager.WasItemSyncedAfter(itemId, timestamp);

            // Assert
            wasSynced.Should().BeFalse();
        }

        [Fact]
        public void GetItemsSyncedAfter_ShouldReturnMatchingItems()
        {
            // Arrange
            var oldTimestamp = DateTime.UtcNow.AddHours(-2);
            
            _stateManager.RecordItemAsync("item1", "/path1.strm").Wait();
            _stateManager.RecordItemAsync("item2", "/path2.strm").Wait();

            // Act
            var items = _stateManager.GetItemsSyncedAfter(oldTimestamp);

            // Assert
            items.Should().Contain("item1");
            items.Should().Contain("item2");
        }

        [Fact]
        public async Task RemoveItemAsync_ShouldRemoveItem()
        {
            // Arrange
            await _stateManager.RecordItemAsync("item1", "/path1.strm");

            // Act
            await _stateManager.RemoveItemAsync("item1");
            var lastSync = _stateManager.GetItemLastSyncTime("item1");

            // Assert
            lastSync.Should().BeNull();
        }

        [Fact]
        public async Task ClearStateAsync_ShouldRemoveAllItems()
        {
            // Arrange
            await _stateManager.RecordItemAsync("item1", "/path1.strm");
            await _stateManager.RecordItemAsync("item2", "/path2.strm");
            await _stateManager.SaveStateAsync();

            // Act
            await _stateManager.ClearStateAsync();
            var stats = _stateManager.GetStatistics();

            // Assert
            stats.TotalItemsTracked.Should().Be(0);
            
            var stateFile = Path.Combine(_tempPath, ".kinopub-sync-state.json");
            File.Exists(stateFile).Should().BeFalse();
        }

        [Fact]
        public async Task CreateBackupAsync_ShouldCreateBackupFile()
        {
            // Arrange
            await _stateManager.RecordItemAsync("item1", "/path1.strm");
            await _stateManager.SaveStateAsync();

            // Act
            var backupPath = await _stateManager.CreateBackupAsync();

            // Assert
            backupPath.Should().NotBeNull();
            File.Exists(backupPath).Should().BeTrue();
            backupPath.Should().Contain(".backup.");
        }

        [Fact]
        public async Task RestoreFromBackupAsync_ShouldRestoreState()
        {
            // Arrange
            await _stateManager.RecordItemAsync("item1", "/path1.strm");
            await _stateManager.SaveStateAsync();
            var backupPath = await _stateManager.CreateBackupAsync();
            
            await _stateManager.ClearStateAsync();

            // Act
            var restored = await _stateManager.RestoreFromBackupAsync(backupPath!);
            var lastSync = _stateManager.GetItemLastSyncTime("item1");

            // Assert
            restored.Should().BeTrue();
            lastSync.Should().NotBeNull();
        }

        [Fact]
        public async Task GetItemsNeedingResyncAsync_WithMissingFiles_ShouldReturnItems()
        {
            // Arrange
            await _stateManager.RecordItemAsync("item1", "/nonexistent/path1.strm");
            await _stateManager.RecordItemAsync("item2", "/nonexistent/path2.strm");

            // Act
            var needsResync = await _stateManager.GetItemsNeedingResyncAsync();

            // Assert
            needsResync.Should().Contain("item1");
            needsResync.Should().Contain("item2");
        }
    }
}
