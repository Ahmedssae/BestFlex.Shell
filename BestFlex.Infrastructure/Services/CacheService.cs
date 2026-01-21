using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using BestFlex.Application.Abstractions;
using Microsoft.Extensions.Logging;

namespace BestFlex.Infrastructure.Services
{
    public sealed class CacheService : ICacheService, IDisposable
    {
        private readonly ConcurrentDictionary<string, CacheItem> _cache = new();
        private readonly ILogger<CacheService> _logger;
        private readonly Timer _cleanupTimer;
        private bool _disposed = false;

        public CacheService(ILogger<CacheService> logger)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            // Cleanup expired items every 5 minutes
            _cleanupTimer = new Timer(CleanupExpiredItems, null, TimeSpan.FromMinutes(5), TimeSpan.FromMinutes(5));
        }

        public async Task<T> GetOrCreateAsync<T>(string key, Func<Task<T>> factory, TimeSpan? expiry = null)
        {
            if (_disposed) throw new ObjectDisposedException(nameof(CacheService));
            
            if (_cache.TryGetValue(key, out var existing) && !existing.IsExpired)
            {
                _logger.LogDebug("Cache hit for key: {Key}", key);
                return (T)existing.Value;
            }

            _logger.LogDebug("Cache miss for key: {Key}", key);
            
            try
            {
                var value = await factory();
                var cacheItem = new CacheItem(value!, expiry ?? TimeSpan.FromMinutes(10)); // Use null-forgiving operator
                _cache.AddOrUpdate(key, cacheItem, (_, _) => cacheItem);
                return value;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating cache item for key: {Key}", key);
                throw;
            }
        }

        public void Remove(string key)
        {
            if (_disposed) return;
            
            if (_cache.TryRemove(key, out _))
            {
                _logger.LogDebug("Removed cache item for key: {Key}", key);
            }
        }

        public void Clear()
        {
            if (_disposed) return;
            
            _cache.Clear();
            _logger.LogDebug("Cache cleared");
        }

        private void CleanupExpiredItems(object? state)
        {
            var expiredKeys = new List<string>();
            foreach (var kvp in _cache)
            {
                if (kvp.Value.IsExpired)
                {
                    expiredKeys.Add(kvp.Key);
                }
            }

            foreach (var key in expiredKeys)
            {
                _cache.TryRemove(key, out _);
            }

            if (expiredKeys.Count > 0)
            {
                _logger.LogDebug("Cleaned up {Count} expired cache items", expiredKeys.Count);
            }
        }

        private sealed class CacheItem
        {
            public object Value { get; }
            public DateTime Expiry { get; }

            public CacheItem(object value, TimeSpan ttl)
            {
                Value = value;
                Expiry = DateTime.UtcNow.Add(ttl);
            }

            public bool IsExpired => DateTime.UtcNow > Expiry;
        }

        public void Dispose()
        {
            if (!_disposed)
            {
                _cleanupTimer?.Dispose();
                _cache.Clear();
                _disposed = true;
            }
        }
    }
}
