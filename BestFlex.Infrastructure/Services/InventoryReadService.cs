using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using BestFlex.Persistence.Data;
using Microsoft.EntityFrameworkCore;

namespace BestFlex.Infrastructure.Services
{
    /// <summary>
    /// Read-only queries for inventory dashboards and reports.
    /// </summary>
    public class InventoryReadService
    {
        private readonly BestFlexDbContext _db;
        public InventoryReadService(BestFlexDbContext db) => _db = db;

        public record LowStockDto(int Id, string Code, string Name, decimal StockQty);

        /// <summary>
        /// Returns items whose StockQty <= threshold, ordered by StockQty then Name.
        /// </summary>
        public Task<List<LowStockDto>> GetLowStockAsync(int threshold, int take, CancellationToken ct = default)
            => _db.Products.AsNoTracking()
                .Where(p => p.StockQty <= threshold)
                .OrderBy(p => p.StockQty).ThenBy(p => p.Name)
                .Select(p => new LowStockDto(p.Id, p.Code, p.Name, p.StockQty))
                .Take(take)
                .ToListAsync(ct);

        public Task<int> CountLowStockAsync(int threshold, CancellationToken ct = default)
            => _db.Products.AsNoTracking()
                .CountAsync(p => p.StockQty <= threshold, ct);

        /// <summary>
        /// Returns ALL low-stock items up to the specified cap.
        /// </summary>
        public Task<List<LowStockDto>> GetAllLowStockAsync(int threshold, int cap, CancellationToken ct = default)
            => _db.Products.AsNoTracking()
                .Where(p => p.StockQty <= threshold)
                .OrderBy(p => p.StockQty).ThenBy(p => p.Name)
                .Select(p => new LowStockDto(p.Id, p.Code, p.Name, p.StockQty))
                .Take(cap)
                .ToListAsync(ct);
    }
}
