using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using BestFlex.Persistence.Data;
using Microsoft.EntityFrameworkCore;
using BestFlex.Shell.Models;
using BestFlex.Infrastructure.Services;

namespace BestFlex.Shell.Services
{
    public sealed class EfSaleDraftHandler : ISaleDraftHandler
    {
        private readonly BestFlexDbContext _db;
        private readonly SellingService _selling;
        private readonly ILastInvoiceTracker _last; // ⬅️ NEW

        public EfSaleDraftHandler(BestFlexDbContext db, SellingService selling, ILastInvoiceTracker last) // ⬅️ ctor updated
        {
            _db = db;
            _selling = selling;
            _last = last;
        }

        public async Task<SaveResult> SaveAsync(SaleDraft draft)
        {
            var customerId = await ResolveCustomerIdAsync(draft.CustomerName);

            var wantedCodes = draft.Lines
                .Where(l => l.ProductId == null && !string.IsNullOrWhiteSpace(l.Code))
                .Select(l => l.Code!.Trim())
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();

            var codeToId = await _db.Products
                .Where(p => wantedCodes.Contains(p.Code))
                .Select(p => new { p.Id, p.Code })
                .ToDictionaryAsync(x => x.Code, x => x.Id, StringComparer.OrdinalIgnoreCase);

            var missingCodes = new List<string>();
            var lineTuples = new List<(int productId, decimal qty, decimal unitPrice)>();

            foreach (var l in draft.Lines.Where(x => (x.ProductId != null) || !string.IsNullOrWhiteSpace(x.Code)))
            {
                int productId;
                if (l.ProductId.HasValue) productId = (int)l.ProductId.Value;
                else if (!string.IsNullOrWhiteSpace(l.Code) && codeToId.TryGetValue(l.Code, out var pid)) productId = pid;
                else { missingCodes.Add(l.Code ?? "(blank)"); continue; }

                lineTuples.Add((productId, l.Qty, l.Price));
            }

            if (missingCodes.Count > 0) return SaveResult.Fail("These product codes were not found: " + string.Join(", ", missingCodes));
            if (lineTuples.Count == 0) return SaveResult.Fail("No valid lines to save.");

            try
            {
                var issuer = "BestFlex User";
                var invoiceId = await _selling.CreateInvoiceAsync(
                    customerAccountId: customerId,
                    issuer: issuer,
                    currency: draft.Currency ?? "USD",
                    lines: lineTuples,
                    description: null,
                    allowNegativeStock: false
                );

                // Remember last invoice for reprint hotkey
                _last.LastInvoiceId = invoiceId;
                _last.When = DateTimeOffset.Now;

                return SaveResult.Ok(invoiceId, "Saved.");
            }
            catch (DbUpdateConcurrencyException)
            {
                return SaveResult.Fail("Stock was changed by another user. Refresh and try again.");
            }
            catch (InvalidOperationException ex)
            {
                return SaveResult.Fail(ex.Message);
            }
            catch (Exception ex)
            {
                return SaveResult.Fail("Unexpected error while saving:\n" + ex.Message);
            }
        }

        private async Task<int> ResolveCustomerIdAsync(string? name)
        {
            if (!string.IsNullOrWhiteSpace(name))
            {
                var existing = await _db.CustomerAccounts
                    .FirstOrDefaultAsync(c => c.Name.ToLower() == name.Trim().ToLower());
                if (existing != null) return existing.Id;

                var created = new BestFlex.Domain.Entities.CustomerAccount { Name = name.Trim(), Balance = 0 };
                _db.CustomerAccounts.Add(created);
                await _db.SaveChangesAsync();
                return created.Id;
            }

            var walkIn = await _db.CustomerAccounts.FirstOrDefaultAsync(c => c.Name == "Walk-in");
            if (walkIn == null)
            {
                walkIn = new BestFlex.Domain.Entities.CustomerAccount { Name = "Walk-in", Balance = 0 };
                _db.CustomerAccounts.Add(walkIn);
                await _db.SaveChangesAsync();
            }
            return walkIn.Id;
        }
    }
}
