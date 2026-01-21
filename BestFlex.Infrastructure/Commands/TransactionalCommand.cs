using System;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using BestFlex.Application.Abstractions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace BestFlex.Infrastructure.Commands
{
    /// <summary>
    /// Enhanced transactional command with locking and idempotency
    /// </summary>
    public sealed class TransactionalCommand : ITransactionalCommand
    {
        private readonly IUnitOfWork _unitOfWork;
        private readonly IExecutionLockService _executionLock;
        private readonly IIdempotencyService _idempotency;
        private readonly ILogger _logger;
        private readonly Func<Task<object>> _execute;
        private readonly Func<bool>? _canExecute;

        public TransactionalCommand(
            IUnitOfWork unitOfWork,
            IExecutionLockService executionLock,
            IIdempotencyService idempotency,
            ILogger logger,
            Func<Task<object>> execute,
            Func<bool>? canExecute = null)
        {
            _unitOfWork = unitOfWork ?? throw new ArgumentNullException(nameof(unitOfWork));
            _executionLock = executionLock ?? throw new ArgumentNullException(nameof(executionLock));
            _idempotency = idempotency ?? throw new ArgumentNullException(nameof(idempotency));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _execute = execute ?? throw new ArgumentNullException(nameof(execute));
            _canExecute = canExecute;
        }

        public async Task<object?> ExecuteAsync(object command)
        {
            if (command == null) throw new ArgumentNullException(nameof(command));

            // Generate operation ID from command
            var operationId = GenerateOperationId(command);
            
            // Check idempotency first
            if (await _idempotency.HasBeenExecutedAsync(operationId))
            {
                var existingResult = await _idempotency.GetExecutedResultAsync(operationId);
                _logger.LogInformation("Command {CommandType} with ID {OperationId} already executed, returning cached result", 
                    command.GetType().Name, operationId);
                return existingResult;
            }

            // Acquire execution lock
            var lockAcquired = await _executionLock.TryAcquireLockAsync(operationId);
            if (!lockAcquired)
            {
                _logger.LogWarning("Failed to acquire execution lock for {CommandType} with ID {OperationId}", 
                    command.GetType().Name, operationId);
                throw new InvalidOperationException($"Command {command.GetType().Name} is already executing");
            }

            try
            {
                await _unitOfWork.BeginAsync();
                
                var result = await _execute();
                
                await _unitOfWork.CommitAsync();
                
                // Mark as executed on success
                await _idempotency.MarkAsExecutedAsync(operationId, result);
                
                _logger.LogInformation("Command {CommandType} with ID {OperationId} executed successfully", 
                    command.GetType().Name, operationId);
                
                return result;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Command {CommandType} with ID {OperationId} failed, rolling back", 
                    command.GetType().Name, operationId);
                
                try
                {
                    await _unitOfWork.RollbackAsync();
                }
                catch (Exception rollbackEx)
                {
                    _logger.LogError(rollbackEx, "Failed to rollback transaction for {CommandType} with ID {OperationId}", 
                        command.GetType().Name, operationId);
                }
                
                throw;
            }
            finally
            {
                await _executionLock.ReleaseLockAsync(operationId);
            }
        }

        private static string GenerateOperationId(object command)
        {
            var commandType = command.GetType();
            var keyProperties = commandType.GetProperties(BindingFlags.Public | BindingFlags.Instance)
                .Where(p => p.PropertyType.IsPrimitive || p.PropertyType == typeof(string) || p.PropertyType == typeof(Guid))
                .OrderBy(p => p.Name)
                .Select(p => $"{p.Name}:{p.GetValue(command) ?? "null"}");
            
            var keyString = string.Join("_", keyProperties);
            var timestamp = DateTime.UtcNow.ToString("yyyyMMddHHmmss");
            
            return $"{commandType.Name}_{keyString}_{timestamp}";
        }
    }
}