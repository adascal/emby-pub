using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Kinopub.Plugin.Api;
using Kinopub.Plugin.Models;
using MediaBrowser.Model.Logging;

namespace Kinopub.Plugin.Library
{
    /// <summary>
    /// Optimized library sync with caching, state management, and parallel processing
    /// </summary>
    public class OptimizedKinopubLibrarySync
    {
        private readonly ILogger _logger;
        private readonly KinopubApiClient _apiClient;
        private readonly LibraryManager _libraryManager;
        private readonly StrmFileGenerator _strmGenerator;
        private readonly EnhancedCache _cache;
        private readonly SyncStateManager _stateManager;
        private readonly SyncProgressTracker _progressTracker;

        private const string CacheKeyBookmarks = "bookmarks_all";
        private const string CacheKeyCollections = "collections_all";
        private const string CacheKeyWatching = "watching_all";

        public OptimizedKinopubLibrarySync(
            ILogger logger,
            KinopubApiClient apiClient,
            string libraryPath,
            string serverUrl)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _apiClient = apiClient ?? throw new ArgumentNullException(nameof(apiClient));

            var config = Plugin.Instance?.Configuration;
            if (config == null)
            {
                throw new InvalidOperationException("Plugin configuration not available");
            }

            _libraryManager = new LibraryManager(_logger, config);
            _strmGenerator = new StrmFileGenerator(_logger, _libraryManager, config);
            _cache = new EnhancedCache(_logger, maxEntries: 1000);
            _stateManager = new SyncStateManager(_logger, libraryPath);
            _progressTracker = new SyncProgressTracker();
        }

        /// <summary>
        /// Performs optimized library sync with all enhancements
        /// </summary>
        public async Task<OptimizedSyncResult> SyncLibraryAsync(
            CancellationToken cancellationToken,
            IProgress<double>? progress = null)
        {
            var result = new OptimizedSyncResult
            {
                StartTime = DateTime.UtcNow
            };

            try
            {
                var config = Plugin.Instance?.Configuration;
                if (config == null)
                {
                    throw new InvalidOperationException("Plugin configuration not available");
                }

                // Validate configuration
                var validationResult = ValidateConfiguration();
                if (!validationResult.IsValid)
                {
                    foreach (var error in validationResult.Errors)
                    {
                        _logger.Error(error);
                        result.Errors.Add(error);
                    }
                    return result;
                }

                // Load sync state
                await _stateManager.LoadStateAsync(cancellationToken);

                // Ensure library structure
                _libraryManager.EnsureLibraryStructure();

                // Collect all items to sync
                var itemsToSync = new List<Item>();

                if (config.SyncBookmarks)
                {
                    var bookmarkItems = await GetBookmarkItemsAsync(cancellationToken);
                    itemsToSync.AddRange(bookmarkItems);
                }

                if (config.SyncCollections)
                {
                    var collectionItems = await GetCollectionItemsAsync(cancellationToken);
                    itemsToSync.AddRange(collectionItems);
                }

                if (config.SyncContinueWatching)
                {
                    var watchingItems = await GetWatchingItemsAsync(cancellationToken);
                    itemsToSync.AddRange(watchingItems);
                }

                // Deduplicate items by ID
                var uniqueItems = itemsToSync
                    .GroupBy(i => i.Id)
                    .Select(g => g.First())
                    .ToList();

                _logger.Info($"Found {uniqueItems.Count} unique items to sync");

                if (uniqueItems.Count == 0)
                {
                    result.EndTime = DateTime.UtcNow;
                    result.Duration = result.EndTime - result.StartTime;
                    return result;
                }

                // Start progress tracking
                _progressTracker.Start(uniqueItems.Count);

                // Process items with batch processor
                var batchSize = config.BatchSize > 0 ? config.BatchSize : 50;
                var maxConcurrent = config.MaxConcurrentOperations > 0 ? config.MaxConcurrentOperations : 4;
                
                var batchProcessor = new BatchProcessor<Item>(_logger, batchSize, maxConcurrent);

                var batchProgress = new Progress<BatchProgress>(p =>
                {
                    progress?.Report(p.PercentComplete);
                });

                var batchResult = await batchProcessor.ProcessBatchesAsync(
                    uniqueItems,
                    async (item, ct) => await ProcessItemAsync(item, result, ct),
                    cancellationToken,
                    batchProgress);

                result.ItemsProcessed = batchResult.TotalProcessed;
                result.ItemsSuccessful = batchResult.TotalSuccessful;
                result.ItemsFailed = batchResult.TotalFailed;
                result.Errors.AddRange(batchResult.Errors);

                // Save sync state
                await _stateManager.SaveStateAsync(cancellationToken);

                // Get final progress
                var finalProgress = _progressTracker.Stop();
                result.CacheHitRate = _cache.GetStatistics().HitRate;
                result.EndTime = DateTime.UtcNow;
                result.Duration = result.EndTime - result.StartTime;

                _logger.Info($"Sync complete: {result.ItemsSuccessful}/{result.ItemsProcessed} successful, " +
                           $"{result.MoviesCreated} movies, {result.SeriesCreated} series, " +
                           $"{result.EpisodesCreated} episodes, cache hit rate: {result.CacheHitRate:P2}");
            }
            catch (Exception ex)
            {
                _logger.Error($"Optimized library sync failed: {ex.Message}");
                result.Errors.Add($"Sync failed: {ex.Message}");
                result.EndTime = DateTime.UtcNow;
                result.Duration = result.EndTime - result.StartTime;
            }

            return result;
        }

        private ValidationResult ValidateConfiguration()
        {
            var result = new ValidationResult();

            var libraryValidation = _libraryManager.ValidateLibraryPath();
            if (!libraryValidation.IsValid)
            {
                result.IsValid = false;
                result.Errors.AddRange(libraryValidation.Errors);
            }

            var strmValidation = _strmGenerator.ValidateConfiguration();
            if (!strmValidation.IsValid)
            {
                result.IsValid = false;
                result.Errors.AddRange(strmValidation.Errors);
            }

            return result;
        }

        private async Task<List<Item>> GetBookmarkItemsAsync(CancellationToken cancellationToken)
        {
            if (_cache.TryGet<List<Item>>(CacheKeyBookmarks, out var cachedItems))
            {
                _logger.Debug("Using cached bookmarks");
                return cachedItems!;
            }

            var items = new List<Item>();
            var bookmarks = await _apiClient.GetBookmarksAsync(cancellationToken);

            foreach (var bookmark in bookmarks.Items)
            {
                if (cancellationToken.IsCancellationRequested)
                    break;

                var bookmarkItems = await _apiClient.GetBookmarkItemsAsync(
                    bookmark.Id.ToString(), 1, 100, cancellationToken);
                items.AddRange(bookmarkItems.Items);
            }

            _cache.Set(CacheKeyBookmarks, items, CacheTier.Hot);
            return items;
        }

        private async Task<List<Item>> GetCollectionItemsAsync(CancellationToken cancellationToken)
        {
            if (_cache.TryGet<List<Item>>(CacheKeyCollections, out var cachedItems))
            {
                _logger.Debug("Using cached collections");
                return cachedItems!;
            }

            var items = new List<Item>();
            var collections = await _apiClient.GetCollectionsAsync(1, 20, cancellationToken);

            foreach (var collection in collections.Items)
            {
                if (cancellationToken.IsCancellationRequested)
                    break;

                var collectionItems = await _apiClient.GetCollectionItemsAsync(
                    collection.Id.ToString(), cancellationToken);
                items.AddRange(collectionItems.Items);
            }

            _cache.Set(CacheKeyCollections, items, CacheTier.Warm);
            return items;
        }

        private async Task<List<Item>> GetWatchingItemsAsync(CancellationToken cancellationToken)
        {
            if (_cache.TryGet<List<Item>>(CacheKeyWatching, out var cachedItems))
            {
                _logger.Debug("Using cached watching list");
                return cachedItems!;
            }

            var watching = await _apiClient.GetWatchingSerialsAsync(cancellationToken);
            var items = watching.Items.ToList();

            _cache.Set(CacheKeyWatching, items, CacheTier.Hot);
            return items;
        }

        private async Task<bool> ProcessItemAsync(
            Item item,
            OptimizedSyncResult result,
            CancellationToken cancellationToken)
        {
            _progressTracker.RecordItemStarted(item.Id.ToString(), item.Title);

            try
            {
                // Check if item needs re-sync
                var lastSync = _stateManager.GetItemLastSyncTime(item.Id.ToString());
                if (lastSync.HasValue)
                {
                    var age = DateTime.UtcNow - lastSync.Value;
                    if (age.TotalHours < 24) // Skip items synced in last 24 hours
                    {
                        _progressTracker.RecordItemSkipped(item.Id.ToString(), "Recently synced");
                        result.ItemsSkipped++;
                        return true;
                    }
                }

                bool success;
                if (item.Type == "serial")
                {
                    success = await ProcessSeriesAsync(item, result, cancellationToken);
                }
                else
                {
                    success = await ProcessMovieAsync(item, result);
                }

                if (success)
                {
                    _progressTracker.RecordItemSuccess(item.Id.ToString());
                    
                    // Record in state manager
                    var filePath = item.Type == "serial"
                        ? _libraryManager.GetSeriesDirectoryPath(item.Id.ToString())
                        : _libraryManager.GetMovieFilePath(item.Id.ToString());
                    
                    await _stateManager.RecordItemAsync(item.Id.ToString(), filePath, null, cancellationToken);
                }
                else
                {
                    _progressTracker.RecordItemFailure(item.Id.ToString(), "Processing failed");
                }

                return success;
            }
            catch (Exception ex)
            {
                _logger.Error($"Error processing item {item.Title} (ID: {item.Id}): {ex.Message}");
                _progressTracker.RecordItemFailure(item.Id.ToString(), ex.Message);
                result.Errors.Add($"Failed to process '{item.Title}': {ex.Message}");
                return false;
            }
        }

        private async Task<bool> ProcessMovieAsync(Item item, OptimizedSyncResult result)
        {
            if (_libraryManager.MovieFileExists(item.Id.ToString()))
            {
                result.ItemsUpdated++;
                return true;
            }

            var generationResult = await _strmGenerator.GenerateMovieStrmFileAsync(
                item.Id.ToString(),
                forceRecreate: false);

            result.ItemsAdded += generationResult.FilesCreated;
            result.ItemsUpdated += generationResult.FilesUpdated;
            result.MoviesCreated += generationResult.FilesCreated;
            result.Errors.AddRange(generationResult.Errors);

            return generationResult.FilesCreated > 0;
        }

        private async Task<bool> ProcessSeriesAsync(
            Item item,
            OptimizedSyncResult result,
            CancellationToken cancellationToken)
        {
            try
            {
                // Try cache first
                var cacheKey = $"item_details_{item.Id}";
                ItemDetails details;

                if (_cache.TryGet<ItemDetails>(cacheKey, out var cachedDetails))
                {
                    _logger.Debug($"Using cached details for series {item.Id}");
                    details = cachedDetails!;
                }
                else
                {
                    var detailsResponse = await _apiClient.GetItemMediaAsync(item.Id.ToString(), cancellationToken);
                    details = detailsResponse.Item;
                    _cache.Set(cacheKey, details, CacheTier.Warm);
                }

                if (details.Seasons == null || details.Seasons.Count == 0)
                {
                    return false;
                }

                var episodes = new List<EpisodeInfo>();
                foreach (var season in details.Seasons)
                {
                    if (season.Episodes == null) continue;

                    foreach (var episode in season.Episodes)
                    {
                        episodes.Add(new EpisodeInfo
                        {
                            EpisodeId = episode.Id.ToString(),
                            SeasonNumber = season.Number,
                            EpisodeNumber = episode.Number,
                            Title = episode.Title
                        });
                    }
                }

                if (episodes.Count == 0)
                {
                    return false;
                }

                var generationResult = await _strmGenerator.GenerateSeriesFilesAsync(
                    item.Id.ToString(),
                    episodes,
                    forceRecreate: false);

                result.ItemsAdded += generationResult.FilesCreated;
                result.ItemsUpdated += generationResult.FilesUpdated;
                result.EpisodesCreated += generationResult.FilesCreated;
                result.Errors.AddRange(generationResult.Errors);

                if (generationResult.FilesCreated > 0)
                {
                    result.SeriesCreated++;
                }

                return generationResult.FilesCreated > 0;
            }
            catch (Exception ex)
            {
                _logger.Error($"Error processing series {item.Title}: {ex.Message}");
                result.Errors.Add($"Failed to process series '{item.Title}': {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Gets current sync progress
        /// </summary>
        public ProgressStatus GetProgress()
        {
            return _progressTracker.GetStatus();
        }

        /// <summary>
        /// Gets cache statistics
        /// </summary>
        public CacheStatistics GetCacheStatistics()
        {
            return _cache.GetStatistics();
        }

        /// <summary>
        /// Clears all caches
        /// </summary>
        public void ClearCaches()
        {
            _cache.Clear();
            _logger.Info("Cleared all caches");
        }
    }

    /// <summary>
    /// Result of optimized sync operation
    /// </summary>
    public class OptimizedSyncResult
    {
        public int ItemsProcessed { get; set; }
        public int ItemsSuccessful { get; set; }
        public int ItemsFailed { get; set; }
        public int ItemsSkipped { get; set; }
        public int ItemsAdded { get; set; }
        public int ItemsUpdated { get; set; }
        public int MoviesCreated { get; set; }
        public int SeriesCreated { get; set; }
        public int EpisodesCreated { get; set; }
        public double CacheHitRate { get; set; }
        public List<string> Errors { get; set; } = new List<string>();
        public DateTime StartTime { get; set; }
        public DateTime EndTime { get; set; }
        public TimeSpan Duration { get; set; }
        public bool IsSuccess => Errors.Count == 0;
    }
}
