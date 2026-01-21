using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using BestFlex.Application.Abstractions;
using BestFlex.Persistence.Data;
using Microsoft.EntityFrameworkCore;

namespace BestFlex.Infrastructure.Services
{
    public sealed class ProductReadService : IProductReadService
    {
        private readonly BestFlexDbContext _db;
        
        public ProductReadService(BestFlexDbContext db) => _db = db;

        public async Task<List<ProductLookupDto>> GetForSalesAsync(CancellationToken ct = default)
        {
            return await _db.Products
                .AsNoTracking()
                .OrderBy(p => p.Code)
                .Select(p => new ProductLookupDto
                {
                    Id = p.Id,
                    Code = p.Code,
                    Name = p.Name,
                    StockQty = p.StockQty,
                    Price = p.Price
                })
                .ToListAsync(ct);
        }
    }
}
