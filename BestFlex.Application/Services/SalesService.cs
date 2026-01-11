using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using BestFlex.Application.Abstractions;
using BestFlex.Application.Contracts.Sales;
using BestFlex.Domain.Entities;
using BestFlex.Persistence.Data;
using Microsoft.EntityFrameworkCore;

namespace BestFlex.Application.Services.Sales
{
    public sealed class SalesService : ISalesService
    {
        private readonly BestFlexDbContext _db;

        // ctor: use BestFlexDbContext (not AppDbContext)
        public SalesService(BestFlexDbContext db) => _db = db;

        public async Task<int> CreateSaleAsync(NewSaleDto dto, CancellationToken ct = default)
        {
            if (dto.Items == null || dto.Items.Count == 0)
                throw new InvalidOperationException("Cannot save an empty sale.");

            const int maxAttempts = 2;
            for (int attempt = 1; attempt <= maxAttempts; attempt++)
            {
                using var tx = await _db.Database.BeginTransactionAsync(ct);

                try
                {
                    var productIds = dto.Items.Select(i => i.ProductId).Distinct().ToList();
                    var products = await _db.Products
                        .Where(p => productIds.Contains(p.Id))
                        .ToDictionaryAsync(p => p.Id, ct);

                    // validate and decrement stock
                    foreach (var line in dto.Items)
                    {
                        if (!products.TryGetValue(line.ProductId, out var p))
                            throw new InvalidOperationException($"Product #{line.ProductId} not found.");

                        if (line.Quantity <= 0)
                            throw new InvalidOperationException($"Quantity must be > 0 for {p.Name}.");

                        if (p.StockQty < line.Quantity)
                            throw new InvalidOperationException(
                                $"Not enough stock for {p.Name}. In stock: {p.StockQty}, requested: {line.Quantity}.");

                        p.StockQty -= line.Quantity;
                        p.UpdatedAt = DateTime.UtcNow;
                        p.Version++; // keep if your Product has 'Version' (it does in your project)
                    }

                    // map to your entity's actual property names
                    var inv = new SellingInvoice
                    {
                        CustomerAccountId = dto.CustomerId ?? 0, // adjust your null handling as needed
                        IssuedAt = dto.InvoiceDate,
                        Currency = dto.Currency,
                        Issuer = "System", // or set from current user service
                        Description = dto.Notes
                    };
                    _db.SellingInvoices.Add(inv);
                    await _db.SaveChangesAsync(ct); // get inv.Id

                    foreach (var line in dto.Items)
                    {
                        _db.SellingInvoiceItems.Add(new SellingInvoiceItem
                        {
                            SellingInvoiceId = inv.Id,
                            ProductId = line.ProductId,
                            Quantity = line.Quantity,
                            UnitPrice = line.UnitPrice
                        });
                    }

                    await _db.SaveChangesAsync(ct);
                    await tx.CommitAsync(ct);
                    return inv.Id;
                }
                catch (DbUpdateConcurrencyException)
                {
                    await tx.RollbackAsync(ct);

                    if (attempt == maxAttempts)
                        throw new InvalidOperationException(
                            "This sale could not be saved because product stock changed at the same time. Please refresh and try again.");

                    // refresh conflicted entries and retry
                    foreach (var entry in _db.ChangeTracker.Entries<Product>())
                    {
                        if (entry.State == EntityState.Modified || entry.State == EntityState.Unchanged)
                        {
                            var dbVals = await entry.GetDatabaseValuesAsync(ct);
                            if (dbVals != null)
                            {
                                entry.OriginalValues.SetValues(dbVals);
                                entry.CurrentValues.SetValues(dbVals);
                                entry.State = EntityState.Unchanged;
                            }
                            else
                            {
                                entry.State = EntityState.Detached;
                            }
                        }
                    }
                }
                catch
                {
                    await tx.RollbackAsync(ct);
                    throw;
                }
            }

            throw new InvalidOperationException("Unexpected save flow break.");
        }
    }
}
