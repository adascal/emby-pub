using System;
using MediaBrowser.Model.Plugins;

namespace Kinopub.Plugin.Configuration
{
    public class PluginConfiguration : BasePluginConfiguration
    {
        public string ApiBaseUrl { get; set; } = "https://api.service-kp.com";
        public string ClientId { get; set; } = "xbmc";
        public string ClientSecret { get; set; } = "cgg3gtifu46urtfp2zp1nqtba0k2ezxh";

        public string? AccessToken { get; set; }
        public string? RefreshToken { get; set; }
        public DateTime TokenExpiry { get; set; }

        public bool EnableAdultContent { get; set; } = false;
        public int DefaultItemsPerPage { get; set; } = 20;
        public string PreferredStreamingType { get; set; } = "http"; // http, hls, hls2, hls4

        // Library Sync Configuration
        public string? LibraryPath { get; set; }
        public string? ServerUrl { get; set; }
        public bool EnableLibrarySync { get; set; } = false;
        public bool SyncBookmarks { get; set; } = true;
        public bool SyncCollections { get; set; } = false;
        public bool SyncContinueWatching { get; set; } = true;

        // Performance Configuration
        public int BatchSize { get; set; } = 50;
        public int MaxConcurrentOperations { get; set; } = 4;
        public int CacheExpirationHotMinutes { get; set; } = 60;
        public int CacheExpirationWarmMinutes { get; set; } = 15;
        public int CacheExpirationColdMinutes { get; set; } = 5;
        public int MaxCacheEntries { get; set; } = 1000;
        public bool EnableIncrementalSync { get; set; } = true;
        public int IncrementalSyncThresholdHours { get; set; } = 24;
        public bool EnableParallelProcessing { get; set; } = true;
    }
}
