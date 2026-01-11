using System;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using BestFlex.Persistence.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace BestFlex.Shell.Views.Pages.Inventory
{
    /// <summary>
    /// ViewModel for ProductsPage that performs data access, filtering and paging.
    /// Keeps code-behind UI-focused.
    /// </summary>
    public sealed class ProductsPageViewModel
    {
        private readonly IServiceProvider _sp;

        public ObservableCollection<ProductRow> Rows { get; } = new();

        public int Page { get; set; }
        public int PageSize { get; set; } = 25;
        public int Total { get; private set; }

        // Filters
        public string CodeFilter { get; set; } = string.Empty;
        public string NameFilter { get; set; } = string.Empty;
        public int? StockMin { get; set; }
        public int? StockMax { get; set; }

        public ProductsPageViewModel(IServiceProvider sp)
        {
            _sp = sp ?? throw new ArgumentNullException(nameof(sp));
        }

        public async Task LoadAsync(CancellationToken ct = default)
        {
            using var scope = _sp.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<BestFlexDbContext>();

            var q = db.Products.AsNoTracking().Select(p => new { p.Id, p.Code, p.Name, p.StockQty });

            var code = (CodeFilter ?? string.Empty).Trim();
            if (!string.IsNullOrEmpty(code)) q = q.Where(p => p.Code.Contains(code));

            var name = (NameFilter ?? string.Empty).Trim();
            if (!string.IsNullOrEmpty(name)) q = q.Where(p => p.Name.Contains(name));

            if (StockMin.HasValue) q = q.Where(p => p.StockQty >= StockMin.Value);
            if (StockMax.HasValue) q = q.Where(p => p.StockQty <= StockMax.Value);

            Total = await q.CountAsync(ct);

            var pageRows = await q
                .OrderBy(p => p.Code)
                .Skip(Page * PageSize)
                .Take(PageSize)
                .ToListAsync(ct);

            Rows.Clear();
            foreach (var r in pageRows)
                Rows.Add(new ProductRow(r.Id, r.Code, r.Name, r.StockQty));
        }

        public sealed record ProductRow(int Id, string Code, string Name, decimal StockQty);
    }
}
