using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace BestFlex.Application.Abstractions
{
    /// <summary>
    /// Cache service for performance optimization.
    /// Provides thread-safe caching with TTL support and automatic cleanup.
    /// This interface is stable and should not be modified without architectural review.
    /// </summary>
    public interface ICacheService
    {
        /// <summary>Get cached item or create if not exists.</summary>
        Task<T> GetOrCreateAsync<T>(string key, Func<Task<T>> factory, TimeSpan? expiry = null);
        
        /// <summary>Remove item from cache.</summary>
        void Remove(string key);
        
        /// <summary>Clear all cached items.</summary>
        void Clear();
    }
}
