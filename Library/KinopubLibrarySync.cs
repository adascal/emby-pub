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
    public class KinopubLibrarySync
    {
        private readonly ILogger _logger;
        private readonly KinopubApiClient _apiClient;
        private readonly LibraryManager _libraryManager;
        private readonly StrmFileGenerator _strmGenerator;

        public KinopubLibrarySync(ILogger logger, KinopubApiClient apiClient, string libraryPath, string serverUrl)
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
        }

        public async Task<SyncResult> SyncLibraryAsync(CancellationToken cancellationToken)
        {
            var result = new SyncResult();
            var startTime = DateTime.UtcNow;

            try
            {
                var config = Plugin.Instance?.Configuration;
                if (config == null)
                {
                    throw new InvalidOperationException("Plugin configuration not available");
                }

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

                _libraryManager.EnsureLibraryStructure();

                if (config.SyncBookmarks)
                {
                    await SyncBookmarksAsync(result, cancellationToken);
                }

                if (config.SyncCollections)
                {
                    await SyncCollectionsAsync(result, cancellationToken);
                }

                if (config.SyncContinueWatching)
                {
                    await SyncContinueWatchingAsync(result, cancellationToken);
                }

                result.Duration = DateTime.UtcNow - startTime;
                result.SyncTime = DateTime.UtcNow;
            }
            catch (Exception ex)
            {
                _logger.Error($"Library sync failed: {ex.Message}");
                result.Errors.Add($"Sync failed: {ex.Message}");
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

        private async Task SyncBookmarksAsync(SyncResult result, CancellationToken cancellationToken)
        {
            try
            {
                var bookmarks = await _apiClient.GetBookmarksAsync(cancellationToken);

                foreach (var bookmark in bookmarks.Items)
                {
                    if (cancellationToken.IsCancellationRequested)
                    {
                        break;
                    }

                    var items = await _apiClient.GetBookmarkItemsAsync(bookmark.Id.ToString(), 1, 100, cancellationToken);

                    foreach (var item in items.Items)
                    {
                        await ProcessItemAsync(item, result, cancellationToken);
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.Error($"Error syncing bookmarks: {ex.Message}");
                result.Errors.Add($"Bookmarks sync error: {ex.Message}");
            }
        }

        private async Task SyncCollectionsAsync(SyncResult result, CancellationToken cancellationToken)
        {
            try
            {
                var collections = await _apiClient.GetCollectionsAsync(1, 20, cancellationToken);

                foreach (var collection in collections.Items)
                {
                    if (cancellationToken.IsCancellationRequested)
                    {
                        break;
                    }

                    var items = await _apiClient.GetCollectionItemsAsync(collection.Id.ToString(), cancellationToken);

                    foreach (var item in items.Items)
                    {
                        await ProcessItemAsync(item, result, cancellationToken);
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.Error($"Error syncing collections: {ex.Message}");
                result.Errors.Add($"Collections sync error: {ex.Message}");
            }
        }

        private async Task SyncContinueWatchingAsync(SyncResult result, CancellationToken cancellationToken)
        {
            try
            {
                var watching = await _apiClient.GetWatchingSerialsAsync(cancellationToken);

                foreach (var item in watching.Items)
                {
                    if (cancellationToken.IsCancellationRequested)
                    {
                        break;
                    }

                    await ProcessItemAsync(item, result, cancellationToken);
                }
            }
            catch (Exception ex)
            {
                _logger.Error($"Error syncing continue watching: {ex.Message}");
                result.Errors.Add($"Continue watching sync error: {ex.Message}");
            }
        }

        private async Task ProcessItemAsync(Item item, SyncResult result, CancellationToken cancellationToken)
        {
            try
            {
                if (item.Type == "serial")
                {
                    await ProcessSeriesAsync(item, result, cancellationToken);
                }
                else
                {
                    await ProcessMovieAsync(item, result);
                }
            }
            catch (Exception ex)
            {
                _logger.Error($"Error processing item {item.Title} (ID: {item.Id}): {ex.Message}");
                result.Errors.Add($"Failed to process '{item.Title}': {ex.Message}");
            }
        }

        private async Task ProcessMovieAsync(Item item, SyncResult result)
        {
            if (_libraryManager.MovieFileExists(item.Id.ToString()))
            {
                result.ItemsUpdated++;
                return;
            }

            var generationResult = await _strmGenerator.GenerateMovieStrmFileAsync(item.Id.ToString(), forceRecreate: false);

            result.ItemsAdded += generationResult.FilesCreated;
            result.ItemsUpdated += generationResult.FilesUpdated;
            result.MoviesCreated += generationResult.FilesCreated;
            result.Errors.AddRange(generationResult.Errors);
        }

        private async Task ProcessSeriesAsync(Item item, SyncResult result, CancellationToken cancellationToken)
        {
            try
            {
                var details = await _apiClient.GetItemMediaAsync(item.Id.ToString(), cancellationToken);

                if (details.Item.Seasons == null || details.Item.Seasons.Count == 0)
                {
                    return;
                }

                var episodes = new List<EpisodeInfo>();
                foreach (var season in details.Item.Seasons)
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
                    return;
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
            }
            catch (Exception ex)
            {
                _logger.Error($"Error processing series {item.Title}: {ex.Message}");
                result.Errors.Add($"Failed to process series '{item.Title}': {ex.Message}");
            }
        }
    }

    public class SyncResult
    {
        public int ItemsAdded { get; set; }
        public int ItemsUpdated { get; set; }
        public int MoviesCreated { get; set; }
        public int SeriesCreated { get; set; }
        public int EpisodesCreated { get; set; }
        public List<string> Errors { get; set; } = new List<string>();
        public TimeSpan Duration { get; set; }
        public DateTime SyncTime { get; set; }
        public bool IsSuccess => Errors.Count == 0;
    }
}
