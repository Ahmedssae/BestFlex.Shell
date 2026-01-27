using System.Threading;
using System.Threading.Tasks;
using BestFlex.Application.Abstractions.Services;
using BestFlex.Application.Contracts.Sales;

namespace BestFlex.Application.Services
{
    // Minimal non-throwing stub implementation to satisfy DI and architectural boundaries.
    public class SalesService : ISalesService
    {
        public SalesService()
        {
        }

        public Task<int> CreateSaleAsync(NewSaleDto dto, CancellationToken ct = default)
        {
            // No business logic here. Return 0 as a harmless invoice id placeholder.
            return Task.FromResult(0);
        }
    }
}
