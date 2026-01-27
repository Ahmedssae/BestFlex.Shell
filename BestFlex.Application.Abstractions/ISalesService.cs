using System.Threading;
using System.Threading.Tasks;
using BestFlex.Application.Abstractions.Contracts.Sales;

namespace BestFlex.Application.Abstractions
{
    public interface ISalesService
    {
        Task<bool> PingAsync(CancellationToken ct = default);
        Task<int> CreateSaleAsync(NewSaleDto dto, CancellationToken ct = default);
    }
}
