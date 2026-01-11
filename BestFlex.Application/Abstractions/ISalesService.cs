using System.Threading;
using System.Threading.Tasks;
using BestFlex.Application.Contracts.Sales;

namespace BestFlex.Application.Abstractions
{
    public interface ISalesService
    {
        /// <summary>
        /// Creates a new sale, decrements stock with optimistic concurrency, and returns the new invoice ID.
        /// </summary>
        Task<int> CreateSaleAsync(NewSaleDto dto, CancellationToken ct = default);
    }
}
