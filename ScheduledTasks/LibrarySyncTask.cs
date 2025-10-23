using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Kinopub.Plugin.Library;
using MediaBrowser.Model.Logging;
using MediaBrowser.Model.Tasks;

namespace Kinopub.Plugin.ScheduledTasks
{
    public class LibrarySyncTask : IScheduledTask
    {
        private readonly ILogger _logger;

        public LibrarySyncTask(ILogManager logManager)
        {
            _logger = logManager.GetLogger(GetType().Name);
        }

        public string Name => "Sync Kinopub Library";
        public string Key => "KinopubLibrarySync";
        public string Description => "Synchronizes Kinopub bookmarks, collections, and continue watching to Emby library";
        public string Category => "Kinopub";

        public async Task Execute(CancellationToken cancellationToken, IProgress<double> progress)
        {
            progress?.Report(0);

            var config = Plugin.Instance?.Configuration;
            if (config == null || !config.EnableLibrarySync)
            {
                _logger.Error("Library sync is not enabled or configuration is not available");
                return;
            }

            if (string.IsNullOrEmpty(config.LibraryPath))
            {
                _logger.Error("LibraryPath is not configured");
                return;
            }

            if (string.IsNullOrEmpty(config.ServerUrl))
            {
                _logger.Error("ServerUrl is not configured");
                return;
            }

            progress?.Report(5);

            var apiClient = Plugin.Instance.GetApiClient();
            if (apiClient == null)
            {
                _logger.Error("API client is not available - authentication may be required");
                return;
            }

            var librarySync = new KinopubLibrarySync(_logger, apiClient, config.LibraryPath, config.ServerUrl);

            var result = await librarySync.SyncLibraryAsync(cancellationToken);

            progress?.Report(100);

            if (result.IsSuccess)
            {
                _logger.Error($"Library sync completed: {result.MoviesCreated} movies, {result.SeriesCreated} series ({result.EpisodesCreated} episodes)");
            }
            else
            {
                _logger.Error($"Library sync completed with {result.Errors.Count} errors");
            }
        }

        public IEnumerable<TaskTriggerInfo> GetDefaultTriggers()
        {
            return new[]
            {
                new TaskTriggerInfo
                {
                    Type = TaskTriggerInfo.TriggerDaily,
                    TimeOfDayTicks = TimeSpan.FromHours(2).Ticks
                }
            };
        }
    }
}
