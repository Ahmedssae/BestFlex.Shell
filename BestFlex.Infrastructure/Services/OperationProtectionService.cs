using System;
using System.Collections.Concurrent;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace BestFlex.Infrastructure.Services
{
    /// <summary>
    /// Provides protection for long-running operations with cancellation support
    /// </summary>
    public class OperationProtectionService : IOperationProtectionService
    {
        private readonly ILogger<OperationProtectionService> _logger;
        private readonly ConcurrentDictionary<string, CancellationTokenSource> _activeOperations = new();
        private readonly ConcurrentDictionary<string, DateTime> _operationStartTimes = new();

        public OperationProtectionService(ILogger<OperationProtectionService> logger)
        {
            _logger = logger;
        }

        public async Task<T> ExecuteOperationAsync<T>(
            Func<CancellationToken, Task<T>> operation,
            string operationName,
            TimeSpan? timeout = null,
            CancellationToken externalCancellationToken = default)
        {
            var correlationId = GenerateCorrelationId();
            var startTime = DateTime.UtcNow;
            var effectiveTimeout = timeout ?? TimeSpan.FromMinutes(5);
            
            using var combinedCts = CancellationTokenSource.CreateLinkedTokenSource(externalCancellationToken);
            combinedCts.CancelAfter(effectiveTimeout);

            // Register the operation
            _activeOperations.TryAdd(correlationId, combinedCts);
            _operationStartTimes.TryAdd(correlationId, startTime);

            try
            {
                _logger.LogInformation("Starting operation {OperationName} [CID:{CorrelationId}] - Timeout: {Timeout}s", 
                    operationName, correlationId, effectiveTimeout.TotalSeconds);

                var result = await operation(combinedCts.Token);
                
                var duration = DateTime.UtcNow - startTime;
                _logger.LogInformation("Operation {OperationName} completed successfully [CID:{CorrelationId}] - Duration: {Duration}ms", 
                    operationName, correlationId, duration.TotalMilliseconds);
                
                return result;
            }
            catch (OperationCanceledException)
            {
                _logger.LogInformation("Operation {OperationName} was cancelled [CID:{CorrelationId}]", operationName, correlationId);
                throw;
            }
            catch (TimeoutException)
            {
                _logger.LogWarning("Operation {OperationName} timed out after {Timeout}s [CID:{CorrelationId}]", 
                    operationName, effectiveTimeout.TotalSeconds, correlationId);
                throw new OperationTimeoutException($"Operation '{operationName}' timed out after {effectiveTimeout.TotalSeconds} seconds", correlationId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Operation {OperationName} failed [CID:{CorrelationId}]", operationName, correlationId);
                throw new OperationFailedException($"Operation '{operationName}' failed", correlationId, ex);
            }
            finally
            {
                // Clean up
                _activeOperations.TryRemove(correlationId, out _);
                _operationStartTimes.TryRemove(correlationId, out _);
            }
        }

        public async Task ExecuteOperationAsync(
            Func<CancellationToken, Task> operation,
            string operationName,
            TimeSpan? timeout = null,
            CancellationToken externalCancellationToken = default)
        {
            await ExecuteOperationAsync(async (ct) => 
            {
                await operation(ct);
                return true; // Return value for Task-based operations
            }, operationName, timeout, externalCancellationToken);
        }

        public bool CancelOperation(string correlationId)
        {
            if (_activeOperations.TryGetValue(correlationId, out var cts))
            {
                _logger.LogInformation("Cancelling operation with correlation ID: {CorrelationId}", correlationId);
                cts.Cancel();
                return true;
            }
            return false;
        }

        public bool CancelAllOperations()
        {
            var cancelledCount = 0;
            foreach (var kvp in _activeOperations)
            {
                if (!kvp.Value.IsCancellationRequested)
                {
                    kvp.Value.Cancel();
                    cancelledCount++;
                }
            }
            
            _logger.LogInformation("Cancelled {Count} active operations", cancelledCount);
            return cancelledCount > 0;
        }

        public OperationStatus GetOperationStatus(string correlationId)
        {
            if (_activeOperations.TryGetValue(correlationId, out var cts))
            {
                var operationStartTime = _operationStartTimes.TryGetValue(correlationId, out var startTime) ? startTime : DateTime.UtcNow;
                var duration = DateTime.UtcNow - operationStartTime;
                
                return new OperationStatus
                {
                    CorrelationId = correlationId,
                    IsActive = !cts.IsCancellationRequested,
                    StartTime = operationStartTime,
                    Duration = duration,
                    IsCancelled = cts.IsCancellationRequested
                };
            }
            
            return new OperationStatus
            {
                CorrelationId = correlationId,
                IsActive = false,
                StartTime = DateTime.MinValue,
                Duration = TimeSpan.Zero,
                IsCancelled = false
            };
        }

        public OperationStatus[] GetActiveOperations()
        {
            return _activeOperations.Select(kvp => 
            {
                var operationStartTime = _operationStartTimes.TryGetValue(kvp.Key, out var startTime) ? startTime : DateTime.UtcNow;
                var duration = DateTime.UtcNow - operationStartTime;
                
                return new OperationStatus
                {
                    CorrelationId = kvp.Key,
                    IsActive = !kvp.Value.IsCancellationRequested,
                    StartTime = operationStartTime,
                    Duration = duration,
                    IsCancelled = kvp.Value.IsCancellationRequested
                };
            }).ToArray();
        }

        public bool IsLongRunningOperation(string operationName, TimeSpan threshold = default)
        {
            var effectiveThreshold = threshold == default ? TimeSpan.FromMilliseconds(300) : threshold;
            return operationName.Contains("Load") || 
                   operationName.Contains("Save") || 
                   operationName.Contains("Process") || 
                   operationName.Contains("Export") ||
                   operationName.Contains("Import");
        }

        private string GenerateCorrelationId()
        {
            var guidStr = Guid.NewGuid().ToString("N");
            return $"OP-{DateTime.UtcNow:yyyyMMdd-HHmmss}-{guidStr.Substring(0, 8).ToUpper()}";
        }
    }

    public interface IOperationProtectionService
    {
        Task<T> ExecuteOperationAsync<T>(Func<CancellationToken, Task<T>> operation, string operationName, TimeSpan? timeout = null, CancellationToken externalCancellationToken = default);
        Task ExecuteOperationAsync(Func<CancellationToken, Task> operation, string operationName, TimeSpan? timeout = null, CancellationToken externalCancellationToken = default);
        bool CancelOperation(string correlationId);
        bool CancelAllOperations();
        OperationStatus GetOperationStatus(string correlationId);
        OperationStatus[] GetActiveOperations();
        bool IsLongRunningOperation(string operationName, TimeSpan threshold = default);
    }

    public class OperationStatus
    {
        public string CorrelationId { get; set; } = string.Empty;
        public bool IsActive { get; set; }
        public DateTime StartTime { get; set; }
        public TimeSpan Duration { get; set; }
        public bool IsCancelled { get; set; }
    }

    public class OperationTimeoutException : Exception
    {
        public string CorrelationId { get; }
        
        public OperationTimeoutException(string message, string correlationId) : base(message)
        {
            CorrelationId = correlationId;
        }
    }

    public class OperationFailedException : Exception
    {
        public string CorrelationId { get; }
        public new Exception? InnerException { get; }
        
        public OperationFailedException(string message, string correlationId, Exception? innerException = null) : base(message, innerException)
        {
            CorrelationId = correlationId;
            InnerException = innerException;
        }
    }
}
