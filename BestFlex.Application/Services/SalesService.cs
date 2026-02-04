using System.Threading;
using System.Threading.Tasks;
using BestFlex.Application.Abstractions;
using BestFlex.Application.Abstractions.Contracts.Sales;
using BestFlex.Domain.Exceptions;

namespace BestFlex.Application.Services
{
    public class SalesService : ISalesService
    {
        public Task<bool> PingAsync(CancellationToken ct = default)
        {
            return Task.FromResult(true);
        }

        public Task<int> CreateSaleAsync(NewSaleDto dto, CancellationToken ct = default)
        {
            // FAKE ERP LOGIC REMOVED - This will be replaced with real implementation in Phase 2
            throw new DomainException("Sales service not implemented yet. Phase 1 - Domain entities only.");
        }
    }
}
