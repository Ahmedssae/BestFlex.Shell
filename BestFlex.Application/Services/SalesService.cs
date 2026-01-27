using System.Threading;
using System.Threading.Tasks;
using BestFlex.Application.Abstractions;
using BestFlex.Application.Abstractions.Contracts.Sales;

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
            // Minimal implementation - return dummy invoice ID
            return Task.FromResult(1);
        }
    }
}
