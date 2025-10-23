using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using MediaBrowser.Model.Logging;

namespace Kinopub.Plugin.Api
{
    /// <summary>
    /// Enhanced caching system with tiered TTLs and LRU eviction.
    /// Provides hot (60min), warm (15min), and cold (5min) cache tiers.
    /// </summary>
    public class EnhancedCache
    {
        private readonly ILogger _logger;
        private readonly ConcurrentDictionary<string, CacheEntry> _cache;
        private readonly int _maxEntries;
        private long _hits;
        private long _misses;

        public EnhancedCache(ILogger logger, int maxEntries = 10000)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _maxEntries = maxEntries;
            _cache = new ConcurrentDictionary<string, CacheEntry>();
        }

        /// <summary>
        /// Gets a value from cache if it exists and hasn't expired.
        /// </summary>
        public bool TryGet<T>(string key, out T? value)
        {
            value = default;

            if (_cache.TryGetValue(key, out var entry))
            {
                if (entry.ExpiresAt > DateTime.UtcNow)
                {
                    entry.LastAccessedAt = DateTime.UtcNow;
                    entry.AccessCount++;
                    System.Threading.Interlocked.Increment(ref _hits);
                    
                    value = (T)entry.Value;
                    return true;
                }
                else
                {
                    // Expired entry, remove it
                    _cache.TryRemove(key, out _);
                }
            }

            System.Threading.Interlocked.Increment(ref _misses);
            return false;
        }

        /// <summary>
        /// Sets a value in cache with the specified tier (hot/warm/cold).
        /// </summary>
        public void Set<T>(string key, T value, CacheTier tier = CacheTier.Warm)
        {
            if (string.IsNullOrEmpty(key) || value == null)
                return;

            // Evict oldest entries if cache is full
            if (_cache.Count >= _maxEntries)
            {
                EvictOldestEntries(_maxEntries / 10); // Evict 10%
            }

            var ttl = GetTtlForTier(tier);
            var entry = new CacheEntry
            {
                Value = value,
                CreatedAt = DateTime.UtcNow,
                ExpiresAt = DateTime.UtcNow.Add(ttl),
                LastAccessedAt = DateTime.UtcNow,
                Tier = tier,
                AccessCount = 0
            };

            _cache.AddOrUpdate(key, entry, (k, old) => entry);
        }

        /// <summary>
        /// Removes a specific key from cache.
        /// </summary>
        public void Remove(string key)
        {
            _cache.TryRemove(key, out _);
        }

        /// <summary>
        /// Clears all entries from cache.
        /// </summary>
        public void Clear()
        {
            _cache.Clear();
            _hits = 0;
            _misses = 0;
        }

        /// <summary>
        /// Gets cache statistics.
        /// </summary>
        public CacheStatistics GetStatistics()
        {
            var totalRequests = _hits + _misses;
            var hitRate = totalRequests > 0 ? (double)_hits / totalRequests * 100 : 0;

            return new CacheStatistics
            {
                TotalEntries = _cache.Count,
                HitCount = _hits,
                MissCount = _misses,
                HitRate = hitRate,
                HotEntries = _cache.Count(e => e.Value.Tier == CacheTier.Hot),
                WarmEntries = _cache.Count(e => e.Value.Tier == CacheTier.Warm),
                ColdEntries = _cache.Count(e => e.Value.Tier == CacheTier.Cold)
            };
        }

        /// <summary>
        /// Evicts expired entries from cache.
        /// </summary>
        public int EvictExpired()
        {
            var now = DateTime.UtcNow;
            var expiredKeys = _cache
                .Where(kvp => kvp.Value.ExpiresAt <= now)
                .Select(kvp => kvp.Key)
                .ToList();

            foreach (var key in expiredKeys)
            {
                _cache.TryRemove(key, out _);
            }

            return expiredKeys.Count;
        }

        /// <summary>
        /// Evicts the oldest (least recently accessed) entries.
        /// Uses LRU (Least Recently Used) eviction strategy.
        /// </summary>
        private void EvictOldestEntries(int count)
        {
            var toEvict = _cache
                .OrderBy(kvp => kvp.Value.LastAccessedAt)
                .Take(count)
                .Select(kvp => kvp.Key)
                .ToList();

            foreach (var key in toEvict)
            {
                _cache.TryRemove(key, out _);
            }
        }

        private TimeSpan GetTtlForTier(CacheTier tier)
        {
            return tier switch
            {
                CacheTier.Hot => TimeSpan.FromMinutes(60),
                CacheTier.Warm => TimeSpan.FromMinutes(15),
                CacheTier.Cold => TimeSpan.FromMinutes(5),
                _ => TimeSpan.FromMinutes(15)
            };
        }

        private class CacheEntry
        {
            public object Value { get; set; } = null!;
            public DateTime CreatedAt { get; set; }
            public DateTime ExpiresAt { get; set; }
            public DateTime LastAccessedAt { get; set; }
            public CacheTier Tier { get; set; }
            public long AccessCount { get; set; }
        }
    }

    public enum CacheTier
    {
        /// <summary>Hot tier: 60 minutes TTL for frequently accessed data</summary>
        Hot,
        /// <summary>Warm tier: 15 minutes TTL for moderately accessed data</summary>
        Warm,
        /// <summary>Cold tier: 5 minutes TTL for rarely accessed data</summary>
        Cold
    }

    public class CacheStatistics
    {
        public int TotalEntries { get; set; }
        public long HitCount { get; set; }
        public long MissCount { get; set; }
        public double HitRate { get; set; }
        public int HotEntries { get; set; }
        public int WarmEntries { get; set; }
        public int ColdEntries { get; set; }
    }
}
