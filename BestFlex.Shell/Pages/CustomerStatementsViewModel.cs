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
    /// <summary>
    /// ViewModel for CustomerStatementsPage. Contains data access and aggregation logic.
    /// </summary>
    public sealed class CustomerStatementsViewModel
    {
        private readonly IServiceProvider _sp;

        public ObservableCollection<CustomerItem> Customers { get; } = new();
        public ObservableCollection<Row> Rows { get; } = new();

        public decimal TotalDebit { get; private set; }
        public decimal TotalCredit { get; private set; }
        public decimal Closing { get; private set; }

        public CustomerStatementsViewModel(IServiceProvider sp)
        {
            _sp = sp ?? throw new ArgumentNullException(nameof(sp));
        }

        public async Task LoadCustomersAsync(CancellationToken ct = default)
        {
            using var scope = _sp.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<BestFlexDbContext>();

            Customers.Clear();
            var items = await db.CustomerAccounts
                .AsNoTracking()
                .OrderBy(c => c.Name)
                .Select(c => new { c.Id, c.Name })
                .ToListAsync(ct);

            foreach (var c in items) Customers.Add(new CustomerItem { Id = c.Id, Name = c.Name });
        }

        /// <summary>
        /// Load statement rows for the given customer id and optional date range.
        /// This contains the SQL and in-memory aggregation logic previously in the code-behind.
        /// </summary>
        public async Task LoadAsync(int customerId, DateTime? from = null, DateTime? to = null, CancellationToken ct = default)
        {
            using var scope = _sp.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<BestFlexDbContext>();

            var invHeadersQuery = db.SellingInvoices.AsNoTracking()
                .Where(i => i.CustomerAccountId == customerId);

            if (from.HasValue) invHeadersQuery = invHeadersQuery.Where(i => i.IssuedAt >= from.Value);
            if (to.HasValue) invHeadersQuery = invHeadersQuery.Where(i => i.IssuedAt <= to.Value.AddDays(1).AddTicks(-1));

            var invHeaders = await invHeadersQuery
                .Select(i => new { i.Id, i.InvoiceNo, i.IssuedAt, i.Description })
                .OrderBy(i => i.IssuedAt)
                .ToListAsync(ct);

            var ids = invHeaders.Select(h => h.Id).ToList();
            var lineItems = await db.SellingInvoiceItems
                .AsNoTracking()
                .Where(it => ids.Contains(it.SellingInvoiceId))
                .Select(it => new { it.SellingInvoiceId, it.Quantity, it.UnitPrice })
                .ToListAsync(ct);

            var amountByInvoiceId = lineItems
                .GroupBy(x => x.SellingInvoiceId)
                .ToDictionary(
                    g => g.Key,
                    g => g.Aggregate(0m, (sum, x) => sum + (x.Quantity * x.UnitPrice)));

            Rows.Clear();
            decimal balance = 0m;
            decimal totalDebit = 0m, totalCredit = 0m;

            foreach (var h in invHeaders)
            {
                var amount = amountByInvoiceId.TryGetValue(h.Id, out var v) ? v : 0m;

                var r = new Row
                {
                    Date = h.IssuedAt.Date,
                    DocNo = h.InvoiceNo,
                    Type = "Invoice",
                    Debit = amount,
                    Credit = 0m,
                    Notes = h.Description
                };
                balance += r.Debit - r.Credit;
                r.Balance = balance;

                totalDebit += r.Debit;
                totalCredit += r.Credit;
                Rows.Add(r);
            }

            TotalDebit = totalDebit;
            TotalCredit = totalCredit;
            Closing = balance;
        }

        public sealed class Row
        {
            public DateTime Date { get; set; }
            public string DocNo { get; set; } = "";
            public string Type { get; set; } = "";
            public decimal Debit { get; set; }
            public decimal Credit { get; set; }
            public decimal Balance { get; set; }
            public string Notes { get; set; } = "";
        }

        public sealed class CustomerItem
        {
            public int Id { get; set; }
            public string Name { get; set; } = "";
        }
    }
}
