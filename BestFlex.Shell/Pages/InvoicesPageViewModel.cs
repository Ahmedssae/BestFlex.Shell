using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using BestFlex.Persistence.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace BestFlex.Shell.Pages
{
    // ViewModel that contains data access and paging logic for InvoicesPage.
    public sealed class InvoicesPageViewModel
    {
        private readonly IServiceProvider _sp;

        public ObservableCollection<InvoiceRow> Rows { get; } = new();

        public int Page { get; set; } = 0;
        public int PageSize { get; set; } = 25;
        public int Total { get; private set; } = 0;

        // Filters
        public string NumberFilter { get; set; } = string.Empty;
        public string CustomerFilter { get; set; } = string.Empty;
        public DateTime? From { get; set; }
        public DateTime? To { get; set; }

        public InvoicesPageViewModel(IServiceProvider sp)
        {
            _sp = sp ?? throw new ArgumentNullException(nameof(sp));
        }

        public async Task LoadAsync(CancellationToken ct = default)
        {
            using var scope = _sp.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<BestFlexDbContext>();

            var q =
                from inv in db.SellingInvoices.AsNoTracking()
                join ca in db.CustomerAccounts.AsNoTracking() on inv.CustomerAccountId equals ca.Id
                select new { inv, ca };

            var num = (NumberFilter ?? string.Empty).Trim();
            if (!string.IsNullOrEmpty(num)) q = q.Where(x => x.inv.InvoiceNo.Contains(num));

            var cust = (CustomerFilter ?? string.Empty).Trim();
            if (!string.IsNullOrEmpty(cust)) q = q.Where(x => EF.Functions.Like(x.ca.Name, $"%{cust}%"));

            if (From.HasValue)
            {
                var from = From.Value.Date;
                q = q.Where(x => x.inv.IssuedAt >= from);
            }
            if (To.HasValue)
            {
                var to = To.Value.Date.AddDays(1).AddTicks(-1);
                q = q.Where(x => x.inv.IssuedAt <= to);
            }

            Total = await q.CountAsync(ct);

            var pageQuery = q
                .OrderByDescending(x => x.inv.IssuedAt)
                .Skip(Page * PageSize)
                .Take(PageSize)
                .Select(x => new
                {
                    x.inv.Id,
                    x.inv.InvoiceNo,
                    x.inv.IssuedAt,
                    CustomerName = x.ca.Name,
                    Currency = x.inv.Currency,
                    Items = db.SellingInvoiceItems.Count(i => i.SellingInvoiceId == x.inv.Id),
                    AmountDouble = db.SellingInvoiceItems
                        .Where(i => i.SellingInvoiceId == x.inv.Id)
                        .Sum(i => (double)(i.Quantity * i.UnitPrice))
                });

            var rows = (await pageQuery.ToListAsync(ct))
                .Select(r => new InvoiceRow(
                    r.Id,
                    r.InvoiceNo,
                    r.IssuedAt,
                    r.CustomerName,
                    r.Items,
                    (decimal)r.AmountDouble,
                    r.Currency ?? "USD"))
                .ToList();

            Rows.Clear();
            foreach (var r in rows) Rows.Add(r);
        }
    }

    public sealed record InvoiceRow(
        int Id,
        string InvoiceNo,
        DateTime IssuedAt,
        string CustomerName,
        int Items,
        decimal Amount,
        string Currency
    );
}
