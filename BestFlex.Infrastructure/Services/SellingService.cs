using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using BestFlex.Domain.Entities;
using BestFlex.Persistence.Data;
using Microsoft.EntityFrameworkCore;

namespace BestFlex.Infrastructure.Services
{
    /// <summary>
    /// Saves selling invoices and updates product stock.
    /// Matches the call sites in EfSaleDraftHandler/NewSaleViewModel:
    /// CreateInvoiceAsync(int customerAccountId, string issuer, string currency,
    ///     IEnumerable<(int productId, decimal qty, decimal unitPrice)> lines,
    ///     string? description = null, bool allowNegativeStock = false)
    /// </summary>
    public class SellingService
    {
        private readonly BestFlexDbContext _db;
        private readonly InvoiceNumberService _nos;

        public SellingService(BestFlexDbContext db, InvoiceNumberService nos)
        {
            _db = db;
            _nos = nos;
        }

        public async Task<int> CreateInvoiceAsync(
            int customerAccountId,
            string issuer,
            string currency,
            IEnumerable<(int productId, decimal qty, decimal unitPrice)> lines,
            string? description = null,
            bool allowNegativeStock = false,
            CancellationToken ct = default)
        {
            if (lines == null) throw new ArgumentNullException(nameof(lines));
            var lineList = lines.ToList();
            if (lineList.Count == 0) throw new InvalidOperationException("No invoice lines were provided.");

            // Summarize by product for stock validation
            var grouped = lineList
                .GroupBy(l => l.productId)
                .Select(g => new
                {
                    ProductId = g.Key,
                    QtyTotal = g.Sum(x => x.qty),
                    Lines = g.ToList()
                })
                .ToList();

            var productIds = grouped.Select(g => g.ProductId).ToList();

            // Load all needed products
            var products = await _db.Products
                .Where(p => productIds.Contains(p.Id))
                .ToDictionaryAsync(p => p.Id, ct);

            // Validate existence & stock
            foreach (var g in grouped)
            {
                if (!products.TryGetValue(g.ProductId, out var p))
                    throw new InvalidOperationException($"Product {g.ProductId} not found.");

                // StockQty is int; qty is decimal. Use ceiling to be safe.
                var requestedInt = (int)Math.Ceiling(g.QtyTotal);
                if (!allowNegativeStock && p.StockQty < requestedInt)
                    throw new InvalidOperationException(
                        $"Not enough stock for {p.Name}. Requested {requestedInt}, available {p.StockQty}.");
            }

            // Header
            var invoice = new SellingInvoice
            {
                InvoiceNo = await _nos.NextAsync("INVS "),
                IssuedAt = DateTime.UtcNow,
                Currency = string.IsNullOrWhiteSpace(currency) ? "USD" : currency,
                Issuer = string.IsNullOrWhiteSpace(issuer) ? "BestFlex User" : issuer,
                Description = description,
                CustomerAccountId = customerAccountId,
                Items = new List<SellingInvoiceItem>()
            };

            // Lines + decrement stock
            foreach (var g in grouped)
            {
                var product = products[g.ProductId];

                foreach (var ln in g.Lines)
                {
                    invoice.Items.Add(new SellingInvoiceItem
                    {
                        ProductId = product.Id,
                        Quantity = ln.qty,
                        UnitPrice = ln.unitPrice
                    });
                }

                var requestedInt = (int)Math.Ceiling(g.QtyTotal);
                product.StockQty -= requestedInt;
                // If you implemented Product.Version concurrency token in DbContext,
                // SaveChanges will include it in the WHERE via [ConcurrencyCheck]/IsConcurrencyToken.
            }

            // Persist inside a transaction (propagates DbUpdateConcurrencyException to caller)
            await using var tx = await _db.Database.BeginTransactionAsync(ct);
            try
            {
                _db.SellingInvoices.Add(invoice);
                await _db.SaveChangesAsync(ct);
                await tx.CommitAsync(ct);
            }
            catch
            {
                await tx.RollbackAsync(ct);
                throw;
            }

            return invoice.Id; // int
        }
    }
}
