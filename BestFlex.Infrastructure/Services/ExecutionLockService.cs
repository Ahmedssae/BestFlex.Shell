using System;
using System.Collections.Concurrent;
using System.Threading;
using System.Threading.Tasks;
using BestFlex.Application.Abstractions;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;

namespace BestFlex.Infrastructure.Services
{
    /// <summary>
    /// In-memory implementation of execution lock service
    /// </summary>
    public sealed class ExecutionLockService : IExecutionLockService
    {
        private readonly ConcurrentDictionary<string, SemaphoreSlim> _locks = new();
        private readonly ILogger<ExecutionLockService> _logger;
        private bool _disposed;

        public ExecutionLockService(ILogger<ExecutionLockService> logger)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public async Task<bool> TryAcquireLockAsync(string operationId, int timeoutMs = 30000)
        {
            if (string.IsNullOrWhiteSpace(operationId))
                throw new ArgumentException("Operation ID cannot be null or empty", nameof(operationId));

            var semaphore = _locks.GetOrAdd(operationId, _ => new SemaphoreSlim(1, 1));
            
            try
            {
                _logger.LogDebug("Attempting to acquire lock for operation {OperationId}", operationId);
                
                var acquired = await semaphore.WaitAsync(timeoutMs);
                
                if (acquired)
                {
                    _logger.LogDebug("Lock acquired for operation {OperationId}", operationId);
                }
                else
                {
                    _logger.LogWarning("Failed to acquire lock for operation {OperationId} within {TimeoutMs}ms", operationId, timeoutMs);
                }
                
                return acquired;
            }
            catch (ObjectDisposedException)
            {
                _logger.LogWarning("Semaphore disposed for operation {OperationId}", operationId);
                return false;
            }
        }

        public Task ReleaseLockAsync(string operationId)
        {
            if (string.IsNullOrWhiteSpace(operationId))
                throw new ArgumentException("Operation ID cannot be null or empty", nameof(operationId));

            if (_locks.TryGetValue(operationId, out var semaphore))
            {
                _logger.LogDebug("Releasing lock for operation {OperationId}", operationId);
                
                semaphore.Release();
                
                // Clean up if no one is waiting
                if (semaphore.CurrentCount == 1)
                {
                    _locks.TryRemove(operationId, out _);
                }
            }
            else
            {
                _logger.LogWarning("Attempted to release non-existent lock for operation {OperationId}", operationId);
            }
            
            return Task.CompletedTask;
        }

        public Task<bool> IsLockedAsync(string operationId)
        {
            if (string.IsNullOrWhiteSpace(operationId))
                return Task.FromResult(false);

            if (_locks.TryGetValue(operationId, out var semaphore))
            {
                return Task.FromResult(semaphore.CurrentCount == 0);
            }
            
            return Task.FromResult(false);
        }

        public void Dispose()
        {
            if (_disposed) return;

            try
            {
                foreach (var semaphore in _locks.Values)
                {
                    semaphore?.Dispose();
                }
                _locks.Clear();
                
                _logger.LogDebug("ExecutionLockService disposed");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error disposing ExecutionLockService");
            }
            finally
            {
                _disposed = true;
            }
        }
    }
}
