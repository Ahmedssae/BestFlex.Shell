using BestFlex.Domain.Entities;
using System.Threading;
using System.Threading.Tasks;

namespace BestFlex.Application.UseCases.SalesOrders
{
    public interface ISalesOrderRepository
    {
        Task<SalesOrder?> GetByIdAsync(int id, CancellationToken cancellationToken);
        Task AddAsync(SalesOrder salesOrder, CancellationToken cancellationToken);
        Task UpdateAsync(SalesOrder salesOrder, CancellationToken cancellationToken);
        Task<int> SaveChangesAsync(CancellationToken cancellationToken);
    }
}
