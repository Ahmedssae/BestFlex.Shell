using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using BestFlex.Application.Abstractions;
using BestFlex.Persistence.Data;

namespace BestFlex.Persistence
{
    public class UnitOfWork : IUnitOfWork
    {
        private readonly BestFlexDbContext _context;
        private IDbContextTransaction? _transaction;

        public UnitOfWork(BestFlexDbContext context)
        {
            _context = context ?? throw new ArgumentNullException(nameof(context));
        }

        public async Task BeginAsync()
        {
            if (_transaction != null)
                throw new InvalidOperationException("Transaction already started");

            _transaction = await _context.Database.BeginTransactionAsync();
        }

        public async Task CommitAsync()
        {
            if (_transaction == null)
                throw new InvalidOperationException("No transaction started");

            try
            {
                await _context.SaveChangesAsync();
                await _transaction.CommitAsync();
            }
            catch
            {
                await RollbackAsync();
                throw;
            }
            finally
            {
                await DisposeTransaction();
            }
        }

        public async Task RollbackAsync()
        {
            if (_transaction == null)
                return;

            try
            {
                await _transaction.RollbackAsync();
            }
            finally
            {
                await DisposeTransaction();
            }
        }

        public async void Dispose()
        {
            await DisposeTransaction();
            _context.Dispose();
        }

        private async Task DisposeTransaction()
        {
            if (_transaction != null)
            {
                await _transaction.DisposeAsync();
                _transaction = null;
            }
        }
    }
}
