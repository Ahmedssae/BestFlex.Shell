using System;
using System.Collections.Concurrent;
using System.Threading.Tasks;
using System.Linq;
using BestFlex.Application.Abstractions;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;

namespace BestFlex.Infrastructure.Services
{
    /// <summary>
    /// In-memory implementation of idempotency service
    /// </summary>
    public sealed class IdempotencyService : IIdempotencyService
    {
        private readonly ConcurrentDictionary<string, ExecutionRecord> _executedOperations = new();
        private readonly ILogger<IdempotencyService> _logger;
        private readonly IMemoryCache _cache;
        private bool _disposed;

        private sealed class ExecutionRecord
        {
            public DateTime ExecutedAt { get; set; }
            public object? Result { get; set; }
        }

        public IdempotencyService(ILogger<IdempotencyService> logger, IMemoryCache cache)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _cache = cache ?? throw new ArgumentNullException(nameof(cache));
        }

        public async Task<bool> HasBeenExecutedAsync(string operationId)
        {
            if (string.IsNullOrWhiteSpace(operationId))
                return false;

            // Check both memory cache and dictionary
            if (_cache.TryGetValue(operationId, out var record))
                return record != null;

            return _executedOperations.ContainsKey(operationId);
        }

        public async Task MarkAsExecutedAsync(string operationId, object? result = null)
        {
            if (string.IsNullOrWhiteSpace(operationId))
                throw new ArgumentException("Operation ID cannot be null or empty", nameof(operationId));

            var record = new ExecutionRecord
            {
                ExecutedAt = DateTime.UtcNow,
                Result = result
            };

            // Store in both cache (with expiration) and dictionary
            _cache.Set(operationId, record, TimeSpan.FromHours(24));
            _executedOperations[operationId] = record;

            _logger.LogDebug("Marked operation {OperationId} as executed at {ExecutedAt}", operationId, record.ExecutedAt);

            await Task.CompletedTask;
        }

        public async Task<object?> GetExecutedResultAsync(string operationId)
        {
            if (string.IsNullOrWhiteSpace(operationId))
                return null;

            // Check cache first, then dictionary
            if (_cache.TryGetValue(operationId, out var cacheRecord) && cacheRecord is ExecutionRecord record)
                return record.Result;

            if (_executedOperations.TryGetValue(operationId, out var dictRecord) && dictRecord is ExecutionRecord dictExecRecord)
                return dictExecRecord.Result;

            return null;
        }

        public async Task ClearExecutionHistoryAsync(string operationId)
        {
            if (string.IsNullOrWhiteSpace(operationId))
                throw new ArgumentException("Operation ID cannot be null or empty", nameof(operationId));

            _cache.Remove(operationId);
            _executedOperations.TryRemove(operationId, out _);

            _logger.LogDebug("Cleared execution history for operation {OperationId}", operationId);

            await Task.CompletedTask;
        }

        public void Dispose()
        {
            if (_disposed) return;

            try
            {
                _executedOperations.Clear();
                _logger.LogDebug("IdempotencyService disposed");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error disposing IdempotencyService");
            }
            finally
            {
                _disposed = true;
            }
        }
    }
}
