using BestFlex.Application.UseCases.SalesOrders;
using BestFlex.Domain.Entities;
using BestFlex.Persistence.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using System;
using System.Data;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace BestFlex.Infrastructure.Repositories
{
    public class SalesOrderTransactionRepository : ISalesOrderTransactionRepository
    {
        private readonly BestFlexDbContext _context;

        public SalesOrderTransactionRepository(BestFlexDbContext context)
        {
            _context = context ?? throw new ArgumentNullException(nameof(context));
        }

        public async Task<SalesOrder?> GetByIdAsync(int id, CancellationToken cancellationToken)
        {
            return await _context.SalesOrders
                .Include(so => so.Lines)
                .FirstOrDefaultAsync(so => so.Id == id, cancellationToken);
        }

        public async Task AddAsync(SalesOrder salesOrder, CancellationToken cancellationToken)
        {
            await _context.SalesOrders.AddAsync(salesOrder, cancellationToken);
            await SaveChangesAsync(cancellationToken);
        }

        public async Task UpdateAsync(SalesOrder salesOrder, CancellationToken cancellationToken)
        {
            // Remove existing lines
            var existingLines = await _context.SalesOrderLines
                .Where(sol => sol.SalesOrderId == salesOrder.Id)
                .ToListAsync(cancellationToken);

            _context.SalesOrderLines.RemoveRange(existingLines);

            // Update the sales order
            _context.SalesOrders.Update(salesOrder);

            // Add new lines
            foreach (var line in salesOrder.Lines)
            {
                await _context.SalesOrderLines.AddAsync(line, cancellationToken);
            }

            await SaveChangesAsync(cancellationToken);
        }

        public async Task<int> SaveChangesAsync(CancellationToken cancellationToken)
        {
            return await _context.SaveChangesAsync(cancellationToken);
        }

        public async Task<IDisposable> BeginTransactionAsync(CancellationToken cancellationToken)
        {
            var transaction = await _context.Database.BeginTransactionAsync(
                IsolationLevel.Serializable, cancellationToken);
            return new TransactionWrapper(transaction);
        }

        private class TransactionWrapper : IDisposable
        {
            private IDbContextTransaction _transaction;
            private bool _disposed = false;

            public TransactionWrapper(IDbContextTransaction transaction)
            {
                _transaction = transaction;
            }

            public void Dispose()
            {
                if (!_disposed)
                {
                    _transaction.Dispose();
                    _disposed = true;
                }
            }
        }
    }
}
