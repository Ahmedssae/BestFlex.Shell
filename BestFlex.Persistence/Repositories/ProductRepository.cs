using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using BestFlex.Domain.Entities;
using BestFlex.Persistence.Data;
using Microsoft.EntityFrameworkCore;

namespace BestFlex.Persistence.Repositories
{
    /// <summary>
    /// Implementation of product repository
    /// </summary>
    public class ProductRepository : IProductRepository
    {
        private readonly BestFlexDbContext _db;

        public ProductRepository(BestFlexDbContext db)
        {
            _db = db ?? throw new ArgumentNullException(nameof(db));
        }

        public async Task<Dictionary<int, Product>> GetByIdsAsync(IEnumerable<int> productIds, CancellationToken cancellationToken = default)
        {
            if (productIds == null) throw new ArgumentNullException(nameof(productIds));

            var products = await _db.Products
                .Where(p => productIds.Contains(p.Id))
                .ToListAsync(cancellationToken);

            return products.ToDictionary(p => p.Id);
        }
    }
}
