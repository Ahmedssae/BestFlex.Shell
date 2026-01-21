using System;
using System.Threading.Tasks;
using System.Data.Common;
using BestFlex.Application.Abstractions;
using BestFlex.Persistence.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace BestFlex.Infrastructure.Transactions
{
    /// <summary>
    /// Entity Framework implementation of Unit of Work
    /// </summary>
    public sealed class EfUnitOfWork : IUnitOfWork
    {
        private readonly BestFlexDbContext _context;
        private readonly ILogger<EfUnitOfWork> _logger;
        private DbTransaction? _transaction;
        private bool _disposed;

        public EfUnitOfWork(BestFlexDbContext context, ILogger<EfUnitOfWork> logger)
        {
            _context = context ?? throw new ArgumentNullException(nameof(context));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public async Task BeginAsync()
        {
            try
            {
                _logger.LogDebug("Beginning database transaction");
                
                if (_context.Database.CurrentTransaction != null)
                {
                    throw new InvalidOperationException("A transaction is already in progress");
                }

                _transaction = (DbTransaction)await _context.Database.BeginTransactionAsync();
                _logger.LogDebug("Database transaction started successfully");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to begin database transaction");
                throw;
            }
        }

        public async Task CommitAsync()
        {
            try
            {
                if (_transaction == null)
                {
                    throw new InvalidOperationException("No transaction in progress");
                }

                _logger.LogDebug("Committing database transaction");
                
                await _context.SaveChangesAsync();
                await _transaction.CommitAsync();
                
                _logger.LogDebug("Database transaction committed successfully");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to commit database transaction");
                await RollbackAsync();
                throw;
            }
        }

        public async Task RollbackAsync()
        {
            try
            {
                if (_transaction == null)
                {
                    _logger.LogDebug("No transaction to rollback");
                    return;
                }

                _logger.LogDebug("Rolling back database transaction");
                
                await _transaction.RollbackAsync();
                
                _logger.LogDebug("Database transaction rolled back successfully");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to rollback database transaction");
                throw;
            }
        }

        public void Dispose()
        {
            if (_disposed) return;

            try
            {
                _transaction?.Dispose();
                _logger.LogDebug("Unit of Work disposed");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error disposing Unit of Work");
            }
            finally
            {
                _disposed = true;
            }
        }
    }
}
