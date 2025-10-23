using System;
using System.Collections.Concurrent;
using System.Threading.Tasks;

namespace Kinopub.Plugin.Api
{
    public class SimpleCache
    {
        private class CacheEntry<T>
        {
            public T Value { get; set; }
            public DateTime ExpiresAt { get; set; }

            public CacheEntry(T value, TimeSpan ttl)
            {
                Value = value;
                ExpiresAt = DateTime.UtcNow.Add(ttl);
            }

            public bool IsExpired => DateTime.UtcNow >= ExpiresAt;
        }

        private readonly ConcurrentDictionary<string, object> _cache = new();
        private readonly TimeSpan _defaultTtl;

        public SimpleCache(TimeSpan? defaultTtl = null)
        {
            _defaultTtl = defaultTtl ?? TimeSpan.FromMinutes(5);
        }

        public async Task<T> GetOrSetAsync<T>(string key, Func<Task<T>> factory, TimeSpan? ttl = null)
        {
            if (_cache.TryGetValue(key, out var cached) && cached is CacheEntry<T> entry)
            {
                if (!entry.IsExpired)
                {
                    return entry.Value;
                }

                // Remove expired entry
                _cache.TryRemove(key, out _);
            }

            var value = await factory();
            var cacheEntry = new CacheEntry<T>(value, ttl ?? _defaultTtl);
            _cache[key] = cacheEntry;

            return value;
        }

        public void Clear()
        {
            _cache.Clear();
        }

        public void Remove(string key)
        {
            _cache.TryRemove(key, out _);
        }

        public void ClearExpired()
        {
            foreach (var kvp in _cache)
            {
                if (kvp.Value is CacheEntry<object> entry && entry.IsExpired)
                {
                    _cache.TryRemove(kvp.Key, out _);
                }
            }
        }
    }
}
