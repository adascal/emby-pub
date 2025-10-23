using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Kinopub.Plugin.Configuration;
using MediaBrowser.Model.Logging;

namespace Kinopub.Plugin.Library
{
    /// <summary>
    /// Generates .strm files containing streaming URLs for Kinopub content.
    /// </summary>
    public class StrmFileGenerator
    {
        private readonly ILogger _logger;
        private readonly LibraryManager _libraryManager;
        private readonly PluginConfiguration _config;

        public StrmFileGenerator(ILogger logger, LibraryManager libraryManager, PluginConfiguration config)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _libraryManager = libraryManager ?? throw new ArgumentNullException(nameof(libraryManager));
            _config = config ?? throw new ArgumentNullException(nameof(config));
        }

        public string GetStreamUrl(string itemId, string? episodeId = null)
        {
            if (string.IsNullOrEmpty(_config.ServerUrl))
            {
                throw new InvalidOperationException("ServerUrl is not configured");
            }

            var serverUrl = _config.ServerUrl.TrimEnd('/');

            if (string.IsNullOrEmpty(episodeId))
            {
                return $"{serverUrl}/Kinopub/Stream/{itemId}?ApiKey={{api_key}}";
            }

            return $"{serverUrl}/Kinopub/Stream/{itemId}?EpisodeId={episodeId}&ApiKey={{api_key}}";
        }

        public async Task<StrmGenerationResult> GenerateMovieStrmFileAsync(string itemId, bool forceRecreate = false)
        {
            var result = new StrmGenerationResult();
            var startTime = DateTime.UtcNow;

            try
            {
                var filePath = _libraryManager.GetMovieFilePath(itemId);

                if (File.Exists(filePath) && !forceRecreate)
                {
                    result.FilesSkipped++;
                    return result;
                }

                var directory = Path.GetDirectoryName(filePath);
                if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
                {
                    Directory.CreateDirectory(directory);
                }

                var url = GetStreamUrl(itemId);
                var tempFile = filePath + ".tmp";

                await File.WriteAllTextAsync(tempFile, url, Encoding.UTF8);
                
                if (File.Exists(filePath))
                {
                    File.Delete(filePath);
                }

                File.Move(tempFile, filePath);

                result.FilesCreated++;
            }
            catch (Exception ex)
            {
                _logger.Error($"Failed to create .strm file for movie {itemId}: {ex.Message}");
                result.Errors.Add($"Movie {itemId}: {ex.Message}");
            }

            result.Duration = DateTime.UtcNow - startTime;
            return result;
        }

        public async Task<StrmGenerationResult> GenerateSeriesFilesAsync(string seriesId, IEnumerable<EpisodeInfo> episodes, bool forceRecreate = false)
        {
            var result = new StrmGenerationResult();
            var startTime = DateTime.UtcNow;

            var episodeList = episodes.ToList();
            if (!episodeList.Any())
            {
                result.Errors.Add($"No episodes provided for series {seriesId}");
                return result;
            }

            try
            {
                var seriesDir = _libraryManager.GetSeriesDirectoryPath(seriesId);
                if (!Directory.Exists(seriesDir))
                {
                    Directory.CreateDirectory(seriesDir);
                }

                var seasonGroups = episodeList.GroupBy(e => e.SeasonNumber);
                foreach (var seasonGroup in seasonGroups)
                {
                    var seasonDir = _libraryManager.GetSeasonDirectoryPath(seriesId, seasonGroup.Key);
                    if (!Directory.Exists(seasonDir))
                    {
                        Directory.CreateDirectory(seasonDir);
                    }

                    foreach (var episode in seasonGroup)
                    {
                        try
                        {
                            var filePath = _libraryManager.GetEpisodeFilePath(seriesId, episode.EpisodeId, episode.SeasonNumber);

                            if (File.Exists(filePath) && !forceRecreate)
                            {
                                result.FilesSkipped++;
                                continue;
                            }

                            var url = GetStreamUrl(seriesId, episode.EpisodeId);
                            var tempFile = filePath + ".tmp";

                            await File.WriteAllTextAsync(tempFile, url, Encoding.UTF8);

                            if (File.Exists(filePath))
                            {
                                File.Delete(filePath);
                            }

                            File.Move(tempFile, filePath);

                            result.FilesCreated++;
                        }
                        catch (Exception ex)
                        {
                            _logger.Error($"Failed to create .strm file for episode {seriesId}-{episode.EpisodeId}: {ex.Message}");
                            result.Errors.Add($"Episode {seriesId}-{episode.EpisodeId}: {ex.Message}");
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.Error($"Failed to create series files for {seriesId}: {ex.Message}");
                result.Errors.Add($"Series {seriesId}: {ex.Message}");
            }

            result.Duration = DateTime.UtcNow - startTime;
            return result;
        }

        public async Task<StrmGenerationResult> GenerateBatchMovieFilesAsync(IEnumerable<string> itemIds, bool forceRecreate = false)
        {
            var result = new StrmGenerationResult();
            var startTime = DateTime.UtcNow;

            foreach (var itemId in itemIds)
            {
                var itemResult = await GenerateMovieStrmFileAsync(itemId, forceRecreate);
                result.FilesCreated += itemResult.FilesCreated;
                result.FilesUpdated += itemResult.FilesUpdated;
                result.FilesSkipped += itemResult.FilesSkipped;
                result.Errors.AddRange(itemResult.Errors);
            }

            result.Duration = DateTime.UtcNow - startTime;
            return result;
        }

        public ValidationResult ValidateConfiguration()
        {
            var result = new ValidationResult { IsValid = true };

            if (string.IsNullOrWhiteSpace(_config.ServerUrl))
            {
                result.IsValid = false;
                result.Errors.Add("ServerUrl is not configured");
            }
            else if (!Uri.TryCreate(_config.ServerUrl, UriKind.Absolute, out var uri))
            {
                result.IsValid = false;
                result.Errors.Add($"ServerUrl is not a valid URL: {_config.ServerUrl}");
            }
            else if (uri.Host == "localhost" || uri.Host == "127.0.0.1")
            {
                result.Warnings.Add("ServerUrl uses localhost - this may not work for remote playback");
            }

            return result;
        }
    }

    public class EpisodeInfo
    {
        public string EpisodeId { get; set; } = string.Empty;
        public int SeasonNumber { get; set; }
        public int EpisodeNumber { get; set; }
        public string? Title { get; set; }
    }

    public class StrmGenerationResult
    {
        public int FilesCreated { get; set; }
        public int FilesUpdated { get; set; }
        public int FilesSkipped { get; set; }
        public List<string> Errors { get; set; } = new List<string>();
        public TimeSpan Duration { get; set; }
    }

    public class ValidationResult
    {
        public bool IsValid { get; set; }
        public List<string> Errors { get; set; } = new List<string>();
        public List<string> Warnings { get; set; } = new List<string>();
    }
}
