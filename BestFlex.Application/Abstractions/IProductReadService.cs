using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace BestFlex.Application.Abstractions
{
    public interface IProductReadService
    {
        Task<List<ProductLookupDto>> GetForSalesAsync(CancellationToken ct = default);
    }

    public record ProductLookupDto
    {
        public int Id { get; init; }
        public string Code { get; init; } = "";
        public string Name { get; init; } = "";
        public decimal StockQty { get; init; }
        public decimal Price { get; init; }
        public string Display => string.IsNullOrWhiteSpace(Code) ? Name : $"{Code} — {Name}";
    }
}
