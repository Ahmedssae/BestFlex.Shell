using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Collections.Generic;
using BestFlex.Application.Abstractions.Statements;
using BestFlex.Persistence.Data;
using Microsoft.EntityFrameworkCore;

namespace BestFlex.Infrastructure.Statements
{
    public sealed class CustomerStatementService : ICustomerStatementService
    {
        private readonly BestFlexDbContext _db;
        public CustomerStatementService(BestFlexDbContext db) => _db = db;

        public async Task<StatementResult> GetAsync(StatementFilter filter, CancellationToken ct = default)
        {
            // Resolve customer by exact Name for now (interface is free-text).
            var cust = await _db.CustomerAccounts
                .AsNoTracking()
                .FirstOrDefaultAsync(c => c.Name == filter.Customer, ct);

            if (cust == null)
                throw new InvalidOperationException($"Customer '{filter.Customer}' not found.");

            var from = filter.From.Date;
            var to = filter.To.Date.AddDays(1).AddTicks(-1);

            // --- Opening balance = sum of invoices BEFORE 'from' ---
            var opening = await _db.SellingInvoices
                .Where(i => i.CustomerAccountId == cust.Id && i.IssuedAt < from)
                .Select(i => i.Items.Sum(x => x.Quantity * x.UnitPrice))
                .DefaultIfEmpty(0m)
                .SumAsync(ct);

            // --- Lines in range: invoices only (payments not modeled yet) ---
            var invoices = await _db.SellingInvoices
                .Where(i => i.CustomerAccountId == cust.Id && i.IssuedAt >= from && i.IssuedAt <= to)
                .OrderBy(i => i.IssuedAt)
                .Select(i => new
                {
                    i.Id,
                    i.InvoiceNo,
                    i.IssuedAt,
                    Amount = i.Items.Sum(x => x.Quantity * x.UnitPrice),
                    i.Currency
                })
                .ToListAsync(ct);

            var lines = new List<StatementLine>();
            decimal running = opening;

            foreach (var inv in invoices)
            {
                running += inv.Amount; // debit
                lines.Add(new StatementLine(
    Date: inv.IssuedAt,
    DocNo: inv.InvoiceNo,
    DocType: "Invoice",
    Debit: inv.Amount,
    Credit: 0m,
    Balance: running,
    Notes: null
));

            }

            // --- Aging (approximate, since payments aren’t modeled yet) ---
            AgingBuckets? aging = null;
            if (filter.IncludeAging)
            {
                var today = DateTime.Today;
                decimal a0_30 = 0, a31_60 = 0, a61_90 = 0, a90p = 0;

                // treat full invoice amount as outstanding
                var allInvoices = await _db.SellingInvoices
                    .Where(i => i.CustomerAccountId == cust.Id)
                    .Select(i => new { i.IssuedAt, Amount = i.Items.Sum(x => x.Quantity * x.UnitPrice) })
                    .ToListAsync(ct);

                foreach (var i in allInvoices)
                {
                    var days = (today - i.IssuedAt.Date).TotalDays;
                    if (days <= 30) a0_30 += i.Amount;
                    else if (days <= 60) a31_60 += i.Amount;
                    else if (days <= 90) a61_90 += i.Amount;
                    else a90p += i.Amount;
                }

                aging = new AgingBuckets(a0_30, a31_60, a61_90, a90p);
            }

            var closing = running;
            return new StatementResult(
                Customer: cust.Name,
                From: from,
                To: to,
                OpeningBalance: opening,
                Lines: lines,
                ClosingBalance: closing,
                Aging: aging
            );
        }
    }
}
