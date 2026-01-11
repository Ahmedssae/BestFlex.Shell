using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using BestFlex.Persistence.Data;
using Microsoft.EntityFrameworkCore;

namespace BestFlex.Infrastructure.Queries
{
    public sealed class DashboardQueryService
    {
        private readonly BestFlexDbContext _db;
        public DashboardQueryService(BestFlexDbContext db) => _db = db;

        public sealed record SalesTotals(decimal Today, decimal ThisMonth);

        public async Task<SalesTotals> GetSalesTotalsAsync()
        {
            var today = DateTime.Today;
            var monthStart = new DateTime(today.Year, today.Month, 1);

            // SQLite-safe: cast to double in SQL, convert to decimal in memory
            var todayDouble = await _db.SellingInvoiceItems
                .Where(x => x.SellingInvoice.IssuedAt >= today)
                .SumAsync(x => (double)x.UnitPrice * (double)x.Quantity);

            var monthDouble = await _db.SellingInvoiceItems
                .Where(x => x.SellingInvoice.IssuedAt >= monthStart)
                .SumAsync(x => (double)x.UnitPrice * (double)x.Quantity);

            return new SalesTotals((decimal)todayDouble, (decimal)monthDouble);
        }

        public sealed record SalesPoint(DateTime Day, decimal Total);

        public async Task<IReadOnlyList<SalesPoint>> GetDailySalesAsync(int lastNDays = 14)
        {
            var start = DateTime.Today.AddDays(-lastNDays + 1);

            // Pre-aggregate per day (SQLite double sum)
            var grouped = await _db.SellingInvoiceItems
                .Where(x => x.SellingInvoice.IssuedAt >= start)
                .GroupBy(x => x.SellingInvoice.IssuedAt.Date)
                .Select(g => new { Day = g.Key, TotalDouble = g.Sum(x => (double)x.UnitPrice * (double)x.Quantity) })
                .ToListAsync();

            var dict = grouped.ToDictionary(x => x.Day, x => (decimal)x.TotalDouble);

            // Ensure continuous days with zeroes
            var res = new List<SalesPoint>(lastNDays);
            for (int i = 0; i < lastNDays; i++)
            {
                var d = start.AddDays(i);
                dict.TryGetValue(d, out var total);
                res.Add(new SalesPoint(d, total));
            }
            return res;
        }

        public sealed record LowStockRow(int Id, string Code, string Name, decimal StockQty);

        public async Task<IReadOnlyList<LowStockRow>> GetLowStockAsync(int threshold = 5, int take = 10)
        {
            return await _db.Products
                .AsNoTracking()
                .Where(p => p.StockQty <= threshold)
                .OrderBy(p => p.StockQty)
                .ThenBy(p => p.Name)
                .Select(p => new LowStockRow(p.Id, p.Code, p.Name, p.StockQty))
                .Take(take)
                .ToListAsync();
        }
    }
}
