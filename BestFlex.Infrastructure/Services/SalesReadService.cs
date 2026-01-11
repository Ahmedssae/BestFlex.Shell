using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using BestFlex.Persistence.Data;
using Microsoft.EntityFrameworkCore;

namespace BestFlex.Infrastructure.Services
{
    /// <summary>
    /// Read-only queries for sales reporting / statements.
    /// Outstanding = current invoice totals (no payments modeled yet).
    /// </summary>
    public sealed class SalesReadService
    {
        private readonly BestFlexDbContext _db;
        public SalesReadService(BestFlexDbContext db) => _db = db;

        public record CustomerOutstandingDto(
            int CustomerAccountId,
            string CustomerName,
            int InvoiceCount,
            decimal Amount);

        public record InvoiceSummaryDto(
            int InvoiceId,
            string InvoiceNo,
            DateTime IssuedAt,
            decimal Amount,
            string Currency);

        /// <summary>
        /// Top customers by outstanding amount (descending).
        /// Uses double for server-side SUM on SQLite, then converts to decimal.
        /// </summary>
        public async Task<List<CustomerOutstandingDto>> GetTopOutstandingAsync(int top, CancellationToken ct = default)
        {
            var q =
                from inv in _db.SellingInvoices.AsNoTracking()
                join item in _db.SellingInvoiceItems.AsNoTracking() on inv.Id equals item.SellingInvoiceId
                join ca in _db.CustomerAccounts.AsNoTracking() on inv.CustomerAccountId equals ca.Id
                group new { inv, item, ca } by new { inv.CustomerAccountId, ca.Name } into g
                select new
                {
                    g.Key.CustomerAccountId,
                    CustomerName = g.Key.Name,
                    InvoiceCount = g.Select(x => x.inv.Id).Distinct().Count(),
                    AmountDouble = g.Sum(x => (double)(x.item.Quantity * x.item.UnitPrice))
                };

            var rows = await q
                .OrderByDescending(x => x.AmountDouble)
                .ThenBy(x => x.CustomerName)
                .Take(top)
                .ToListAsync(ct);

            // Positional construction (no named args)
            return rows
                .Select(x => new CustomerOutstandingDto(
                    x.CustomerAccountId,
                    x.CustomerName,
                    x.InvoiceCount,
                    (decimal)x.AmountDouble))
                .ToList();
        }

        /// <summary>
        /// Invoice summaries for a specific customer (newest first).
        /// Uses double for SQLite SUM, then converts to decimal for the DTO.
        /// </summary>
        public async Task<List<InvoiceSummaryDto>> GetInvoicesForCustomerAsync(int customerAccountId, CancellationToken ct = default)
        {
            var q =
                from inv in _db.SellingInvoices.AsNoTracking()
                join item in _db.SellingInvoiceItems.AsNoTracking() on inv.Id equals item.SellingInvoiceId
                where inv.CustomerAccountId == customerAccountId
                group item by new { inv.Id, inv.InvoiceNo, inv.IssuedAt, inv.Currency } into g
                select new
                {
                    g.Key.Id,
                    g.Key.InvoiceNo,
                    g.Key.IssuedAt,
                    g.Key.Currency,
                    AmountDouble = g.Sum(it => (double)(it.Quantity * it.UnitPrice))
                };

            var rows = await q
                .OrderByDescending(x => x.IssuedAt)
                .ToListAsync(ct);

            return rows
                .Select(x => new InvoiceSummaryDto(
                    x.Id,
                    x.InvoiceNo,
                    x.IssuedAt,
                    (decimal)x.AmountDouble,
                    x.Currency ?? "USD"))
                .ToList();
        }
    }
}
