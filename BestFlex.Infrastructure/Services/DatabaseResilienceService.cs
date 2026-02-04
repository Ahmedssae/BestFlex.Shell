using System;
using System.Data;
using System.Data.Common;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using BestFlex.Application.Abstractions;
using BestFlex.Persistence.Data;

namespace BestFlex.Infrastructure.Services
{
    /// <summary>
    /// Provides database resilience with retry policies and failure detection
    /// </summary>
    public class DatabaseResilienceService : IDatabaseResilienceService
    {
        private readonly ILogger<DatabaseResilienceService> _logger;
        private readonly IServiceProvider _serviceProvider;
        private const int MaxRetryAttempts = 3;
        private static readonly TimeSpan[] RetryDelays = { TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(2), TimeSpan.FromSeconds(4) };

        public DatabaseResilienceService(
            ILogger<DatabaseResilienceService> logger,
            IServiceProvider serviceProvider)
        {
            _logger = logger;
            _serviceProvider = serviceProvider;
        }

        public async Task<T> ExecuteWithRetryAsync<T>(
            Func<Task<T>> operation,
            string operationName,
            CancellationToken cancellationToken = default)
        {
            Exception? lastException = null;
            
            for (int attempt = 1; attempt <= MaxRetryAttempts; attempt++)
            {
                try
                {
                    _logger.LogDebug("Executing database operation {OperationName}, attempt {Attempt}/{MaxAttempts}", 
                        operationName, attempt, MaxRetryAttempts);
                    
                    var result = await operation();
                    
                    if (attempt > 1)
                    {
                        _logger.LogInformation("Database operation {OperationName} succeeded after {Attempt} attempts", 
                            operationName, attempt);
                    }
                    
                    return result;
                }
                catch (Exception ex) when (IsTransientDatabaseError(ex))
                {
                    lastException = ex;
                    _logger.LogWarning(ex, "Database operation {OperationName} failed on attempt {Attempt}/{MaxAttempts}: {ErrorType}", 
                        operationName, attempt, MaxRetryAttempts, ex.GetType().Name);
                    
                    if (attempt < MaxRetryAttempts)
                    {
                        var delay = RetryDelays[attempt - 1];
                        _logger.LogDebug("Retrying database operation {OperationName} in {Delay}ms", operationName, delay.TotalMilliseconds);
                        await Task.Delay(delay, cancellationToken);
                    }
                }
                catch (Exception ex)
                {
                    // Non-transient errors should not be retried
                    _logger.LogError(ex, "Database operation {OperationName} failed with non-transient error: {ErrorType}", 
                        operationName, ex.GetType().Name);
                    throw;
                }
            }
            
            _logger.LogError(lastException, "Database operation {OperationName} failed after {MaxAttempts} attempts", 
                operationName, MaxRetryAttempts);
            throw new DatabaseResilienceException($"Database operation '{operationName}' failed after {MaxRetryAttempts} attempts", lastException!);
        }

        public async Task ExecuteWithRetryAsync(
            Func<Task> operation,
            string operationName,
            CancellationToken cancellationToken = default)
        {
            Exception? lastException = null;
            
            for (int attempt = 1; attempt <= MaxRetryAttempts; attempt++)
            {
                try
                {
                    _logger.LogDebug("Executing database operation {OperationName}, attempt {Attempt}/{MaxAttempts}", 
                        operationName, attempt, MaxRetryAttempts);
                    
                    await operation();
                    
                    if (attempt > 1)
                    {
                        _logger.LogInformation("Database operation {OperationName} succeeded after {Attempt} attempts", 
                            operationName, attempt);
                    }
                    
                    return;
                }
                catch (Exception ex) when (IsTransientDatabaseError(ex))
                {
                    lastException = ex;
                    _logger.LogWarning(ex, "Database operation {OperationName} failed on attempt {Attempt}/{MaxAttempts}: {ErrorType}", 
                        operationName, attempt, MaxRetryAttempts, ex.GetType().Name);
                    
                    if (attempt < MaxRetryAttempts)
                    {
                        var delay = RetryDelays[attempt - 1];
                        _logger.LogDebug("Retrying database operation {OperationName} in {Delay}ms", operationName, delay.TotalMilliseconds);
                        await Task.Delay(delay, cancellationToken);
                    }
                }
                catch (Exception ex)
                {
                    // Non-transient errors should not be retried
                    _logger.LogError(ex, "Database operation {OperationName} failed with non-transient error: {ErrorType}", 
                        operationName, ex.GetType().Name);
                    throw;
                }
            }
            
            _logger.LogError(lastException, "Database operation {OperationName} failed after {MaxAttempts} attempts", 
                operationName, MaxRetryAttempts);
            throw new DatabaseResilienceException($"Database operation '{operationName}' failed after {MaxRetryAttempts} attempts", lastException!);
        }

        public async Task<DatabaseHealthStatus> CheckDatabaseHealthAsync(CancellationToken cancellationToken = default)
        {
            try
            {
                using var scope = _serviceProvider.CreateScope();
                var dbContext = scope.ServiceProvider.GetRequiredService<BestFlexDbContext>();
                
                // Test basic connectivity with a simple query
                var startTime = DateTime.UtcNow;
                await dbContext.Database.CanConnectAsync(cancellationToken);
                var responseTime = DateTime.UtcNow - startTime;
                
                // Test actual query execution
                await dbContext.Database.ExecuteSqlRawAsync("SELECT 1", cancellationToken);
                
                return new DatabaseHealthStatus
                {
                    IsHealthy = true,
                    ResponseTime = responseTime,
                    LastCheck = DateTime.UtcNow,
                    ErrorMessage = null
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Database health check failed");
                return new DatabaseHealthStatus
                {
                    IsHealthy = false,
                    ResponseTime = TimeSpan.MaxValue,
                    LastCheck = DateTime.UtcNow,
                    ErrorMessage = ex.Message
                };
            }
        }

        private bool IsTransientDatabaseError(Exception exception)
        {
            return exception switch
            {
                TimeoutException => true,
                DbException dbEx when IsTransientDbException(dbEx) => true,
                InvalidOperationException opEx when opEx.Message.Contains("timeout", StringComparison.OrdinalIgnoreCase) => true,
                _ => false
            };
        }

        private bool IsTransientDbException(DbException exception)
        {
            var errorMessage = exception.Message;
            
            // Check for common transient error patterns
            return errorMessage.Contains("timeout", StringComparison.OrdinalIgnoreCase) ||
                   errorMessage.Contains("connection", StringComparison.OrdinalIgnoreCase) ||
                   errorMessage.Contains("deadlock", StringComparison.OrdinalIgnoreCase) ||
                   errorMessage.Contains("SQLITE_BUSY") ||
                   errorMessage.Contains("SQLITE_LOCKED") ||
                   errorMessage.Contains("08001") || // PostgreSQL connection errors
                   errorMessage.Contains("08006") || // PostgreSQL connection failure
                   errorMessage.Contains("53000") || // PostgreSQL insufficient resources
                   errorMessage.Contains("4060") ||  // SQL Server login failed
                   errorMessage.Contains("40197") || // SQL Server connection failed
                   errorMessage.Contains("40501") || // SQL Server service not available
                   errorMessage.Contains("40613");   // SQL Server database not available
        }
    }

    public interface IDatabaseResilienceService
    {
        Task<T> ExecuteWithRetryAsync<T>(Func<Task<T>> operation, string operationName, CancellationToken cancellationToken = default);
        Task ExecuteWithRetryAsync(Func<Task> operation, string operationName, CancellationToken cancellationToken = default);
        Task<DatabaseHealthStatus> CheckDatabaseHealthAsync(CancellationToken cancellationToken = default);
    }

    public class DatabaseResilienceException : Exception
    {
        public DatabaseResilienceException(string message, Exception innerException) : base(message, innerException)
        {
        }
    }

    public class DatabaseHealthStatus
    {
        public bool IsHealthy { get; set; }
        public TimeSpan ResponseTime { get; set; }
        public DateTime LastCheck { get; set; }
        public string? ErrorMessage { get; set; }
    }
}
