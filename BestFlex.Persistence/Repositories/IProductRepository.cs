using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using BestFlex.Domain.Entities;

namespace BestFlex.Persistence.Repositories
{
    /// <summary>
    /// Repository for product operations
    /// </summary>
    public interface IProductRepository
    {
        /// <summary>
        /// Gets products by their IDs
        /// </summary>
        /// <param name="productIds">Product IDs to query</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>Dictionary of products keyed by ID</returns>
        Task<Dictionary<int, Product>> GetByIdsAsync(IEnumerable<int> productIds, CancellationToken cancellationToken = default);
    }
}
