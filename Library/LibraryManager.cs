using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Kinopub.Plugin.Configuration;
using MediaBrowser.Model.Logging;

namespace Kinopub.Plugin.Library
{
    /// <summary>
    /// Manages library directory structure and file paths for Kinopub .strm files.
    /// </summary>
    public class LibraryManager
    {
        private readonly ILogger _logger;
        private readonly PluginConfiguration _config;

        private const string MoviesFolder = "Movies";
        private const string TVShowsFolder = "TV Shows";
        private const string StrmExtension = ".strm";
        private const string KinopubPrefix = "kinopub-";

        public LibraryManager(ILogger logger, PluginConfiguration config)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _config = config ?? throw new ArgumentNullException(nameof(config));
        }

        public void EnsureLibraryStructure()
        {
            if (string.IsNullOrEmpty(_config.LibraryPath))
            {
                throw new InvalidOperationException("LibraryPath is not configured");
            }

            try
            {
                var moviesPath = Path.Combine(_config.LibraryPath, MoviesFolder);
                var tvShowsPath = Path.Combine(_config.LibraryPath, TVShowsFolder);

                if (!Directory.Exists(moviesPath))
                {
                    Directory.CreateDirectory(moviesPath);
                }

                if (!Directory.Exists(tvShowsPath))
                {
                    Directory.CreateDirectory(tvShowsPath);
                }
            }
            catch (Exception ex)
            {
                _logger.Error($"Failed to create library structure: {ex.Message}");
                throw;
            }
        }

        public string GetMovieFilePath(string itemId)
        {
            if (string.IsNullOrEmpty(itemId))
            {
                throw new ArgumentException("Item ID cannot be empty", nameof(itemId));
            }

            var fileName = $"{KinopubPrefix}{itemId}{StrmExtension}";
            return Path.Combine(_config.LibraryPath, MoviesFolder, fileName);
        }

        public string GetEpisodeFilePath(string seriesId, string episodeId, int seasonNumber)
        {
            if (string.IsNullOrEmpty(seriesId))
            {
                throw new ArgumentException("Series ID cannot be empty", nameof(seriesId));
            }

            if (string.IsNullOrEmpty(episodeId))
            {
                throw new ArgumentException("Episode ID cannot be empty", nameof(episodeId));
            }

            if (seasonNumber < 1)
            {
                throw new ArgumentException("Season number must be >= 1", nameof(seasonNumber));
            }

            var seriesFolder = $"{KinopubPrefix}{seriesId}";
            var seasonFolder = $"Season {seasonNumber:D2}";
            var fileName = $"{KinopubPrefix}{seriesId}-{episodeId}{StrmExtension}";

            return Path.Combine(_config.LibraryPath, TVShowsFolder, seriesFolder, seasonFolder, fileName);
        }

        public string GetSeriesDirectoryPath(string seriesId)
        {
            if (string.IsNullOrEmpty(seriesId))
            {
                throw new ArgumentException("Series ID cannot be empty", nameof(seriesId));
            }

            var seriesFolder = $"{KinopubPrefix}{seriesId}";
            return Path.Combine(_config.LibraryPath, TVShowsFolder, seriesFolder);
        }

        public string GetSeasonDirectoryPath(string seriesId, int seasonNumber)
        {
            if (string.IsNullOrEmpty(seriesId))
            {
                throw new ArgumentException("Series ID cannot be empty", nameof(seriesId));
            }

            if (seasonNumber < 1)
            {
                throw new ArgumentException("Season number must be >= 1", nameof(seasonNumber));
            }

            var seriesPath = GetSeriesDirectoryPath(seriesId);
            var seasonFolder = $"Season {seasonNumber:D2}";
            return Path.Combine(seriesPath, seasonFolder);
        }

        public bool MovieFileExists(string itemId)
        {
            try
            {
                var filePath = GetMovieFilePath(itemId);
                return File.Exists(filePath);
            }
            catch (Exception ex)
            {
                _logger.Error($"Error checking movie file existence for {itemId}: {ex.Message}");
                return false;
            }
        }

        public bool EpisodeFileExists(string seriesId, string episodeId, int seasonNumber)
        {
            try
            {
                var filePath = GetEpisodeFilePath(seriesId, episodeId, seasonNumber);
                return File.Exists(filePath);
            }
            catch (Exception ex)
            {
                _logger.Error($"Error checking episode file existence for {seriesId}-{episodeId}: {ex.Message}");
                return false;
            }
        }

        public LibraryValidationResult ValidateLibraryPath()
        {
            var result = new LibraryValidationResult { IsValid = true };

            if (string.IsNullOrWhiteSpace(_config.LibraryPath))
            {
                result.IsValid = false;
                result.Errors.Add("LibraryPath is not configured");
                return result;
            }

            if (!Directory.Exists(_config.LibraryPath))
            {
                result.Warnings.Add($"LibraryPath does not exist: {_config.LibraryPath}");
                result.Warnings.Add("It will be created when sync runs");
            }

            try
            {
                var testFile = Path.Combine(_config.LibraryPath, ".kinopub_test");
                Directory.CreateDirectory(_config.LibraryPath);
                File.WriteAllText(testFile, "test");
                File.Delete(testFile);
            }
            catch (Exception ex)
            {
                result.IsValid = false;
                result.Errors.Add($"LibraryPath is not writable: {ex.Message}");
            }

            return result;
        }

        public LibraryStatistics GetLibraryStatistics()
        {
            var stats = new LibraryStatistics();

            if (string.IsNullOrEmpty(_config.LibraryPath) || !Directory.Exists(_config.LibraryPath))
            {
                return stats;
            }

            try
            {
                var moviesPath = Path.Combine(_config.LibraryPath, MoviesFolder);
                if (Directory.Exists(moviesPath))
                {
                    var movieFiles = Directory.GetFiles(moviesPath, $"{KinopubPrefix}*{StrmExtension}");
                    stats.MovieFiles = movieFiles.Length;
                    stats.TotalFiles += movieFiles.Length;
                    stats.TotalSizeBytes += movieFiles.Sum(f => new FileInfo(f).Length);
                }

                var tvShowsPath = Path.Combine(_config.LibraryPath, TVShowsFolder);
                if (Directory.Exists(tvShowsPath))
                {
                    var seriesDirs = Directory.GetDirectories(tvShowsPath, $"{KinopubPrefix}*");
                    stats.SeriesDirectories = seriesDirs.Length;

                    foreach (var seriesDir in seriesDirs)
                    {
                        var episodeFiles = Directory.GetFiles(seriesDir, $"{KinopubPrefix}*{StrmExtension}", SearchOption.AllDirectories);
                        stats.EpisodeFiles += episodeFiles.Length;
                        stats.TotalFiles += episodeFiles.Length;
                        stats.TotalSizeBytes += episodeFiles.Sum(f => new FileInfo(f).Length);
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.Error($"Error calculating library statistics: {ex.Message}");
            }

            return stats;
        }

        public void CleanupEmptyDirectories()
        {
            if (string.IsNullOrEmpty(_config.LibraryPath) || !Directory.Exists(_config.LibraryPath))
            {
                return;
            }

            try
            {
                CleanupEmptyDirectoriesRecursive(_config.LibraryPath);
            }
            catch (Exception ex)
            {
                _logger.Error($"Error during directory cleanup: {ex.Message}");
            }
        }

        private void CleanupEmptyDirectoriesRecursive(string path)
        {
            try
            {
                foreach (var directory in Directory.GetDirectories(path))
                {
                    CleanupEmptyDirectoriesRecursive(directory);

                    if (!Directory.EnumerateFileSystemEntries(directory).Any())
                    {
                        Directory.Delete(directory);
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.Error($"Error cleaning directory {path}: {ex.Message}");
            }
        }
    }

    public class LibraryValidationResult
    {
        public bool IsValid { get; set; }
        public List<string> Errors { get; set; } = new List<string>();
        public List<string> Warnings { get; set; } = new List<string>();
    }

    public class LibraryStatistics
    {
        public int TotalFiles { get; set; }
        public int MovieFiles { get; set; }
        public int SeriesDirectories { get; set; }
        public int EpisodeFiles { get; set; }
        public long TotalSizeBytes { get; set; }
    }
}
