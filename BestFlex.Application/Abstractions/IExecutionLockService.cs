using System;
using System.Threading;
using System.Threading.Tasks;

namespace BestFlex.Application.Abstractions
{
    /// <summary>
    /// Service for preventing concurrent command execution
    /// </summary>
    public interface IExecutionLockService : IDisposable
    {
        /// <summary>
        /// Attempts to acquire an execution lock for a specific operation
        /// </summary>
        /// <param name="operationId">Unique identifier for the operation</param>
        /// <param name="timeoutMs">Timeout in milliseconds</param>
        /// <returns>True if lock acquired, false if already locked</returns>
        Task<bool> TryAcquireLockAsync(string operationId, int timeoutMs = 30000);

        /// <summary>
        /// Releases an execution lock
        /// </summary>
        /// <param name="operationId">Unique identifier for the operation</param>
        Task ReleaseLockAsync(string operationId);

        /// <summary>
        /// Checks if an operation is currently locked
        /// </summary>
        /// <param name="operationId">Unique identifier for the operation</param>
        /// <returns>True if locked, false otherwise</returns>
        Task<bool> IsLockedAsync(string operationId);
    }

    /// <summary>
    /// Service for ensuring idempotent operations
    /// </summary>
    public interface IIdempotencyService
    {
        /// <summary>
        /// Checks if an operation has already been executed
        /// </summary>
        /// <param name="operationId">Unique identifier for the operation</param>
        /// <returns>True if already executed, false otherwise</returns>
        Task<bool> HasBeenExecutedAsync(string operationId);

        /// <summary>
        /// Marks an operation as executed
        /// </summary>
        /// <param name="operationId">Unique identifier for the operation</param>
        /// <param name="result">Optional result to store</param>
        Task MarkAsExecutedAsync(string operationId, object? result = null);

        /// <summary>
        /// Gets the stored result of a previously executed operation
        /// </summary>
        /// <param name="operationId">Unique identifier for the operation</param>
        /// <returns>Stored result or null</returns>
        Task<object?> GetExecutedResultAsync(string operationId);

        /// <summary>
        /// Clears execution history for an operation (for testing/admin)
        /// </summary>
        /// <param name="operationId">Unique identifier for the operation</param>
        Task ClearExecutionHistoryAsync(string operationId);
    }
}
