using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace BestFlex.Application.Abstractions
{
    public interface ICustomerReadService
    {
        Task<List<CustomerLookupDto>> GetForSalesAsync(CancellationToken ct = default);
    }

    public record CustomerLookupDto
    {
        public int Id { get; init; }
        public string Name { get; init; } = "";
    }
}
