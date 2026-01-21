using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using BestFlex.Application.Abstractions;
using BestFlex.Application.Contracts.Sales;
using BestFlex.Domain.Entities;
using BestFlex.Persistence.Data;
using BestFlex.Persistence.Repositories;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace BestFlex.Infrastructure.Services.Sales
{
    public sealed class SalesService : ISalesService
    {
        private readonly BestFlexDbContext _db;
        private readonly IAccountingService _accountingService;
        private readonly IStockValidationService _stockValidationService;
        private readonly IStockRepository _stockRepository;
        private readonly ILogger<SalesService> _logger;

        // ctor: use BestFlexDbContext (not AppDbContext)
        public SalesService(
            BestFlexDbContext db,
            IAccountingService accountingService,
            IStockValidationService stockValidationService,
            IStockRepository stockRepository,
            ILogger<SalesService> logger)
        {
            _db = db ?? throw new ArgumentNullException(nameof(db));
            _accountingService = accountingService ?? throw new ArgumentNullException(nameof(accountingService));
            _stockValidationService = stockValidationService ?? throw new ArgumentNullException(nameof(stockValidationService));
            _stockRepository = stockRepository ?? throw new ArgumentNullException(nameof(stockRepository));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public async Task<int> CreateSaleAsync(NewSaleDto dto, CancellationToken ct = default)
        {
            if (dto.Items == null || dto.Items.Count == 0)
                throw new InvalidOperationException("Cannot save an empty sale.");

            // Validate stock first
            var stockValidation = await _stockValidationService.ValidateStockAsync(dto.Items, ct);
            if (!stockValidation.IsValid)
            {
                var errorMessages = stockValidation.Errors.Select(e => 
                    $"{e.ProductName}: {e.Error} (Requested: {e.RequestedQuantity}, Available: {e.AvailableQuantity})");
                throw new InvalidOperationException(string.Join("; ", errorMessages));
            }

            // Reserve stock
            var reservation = await _stockValidationService.ReserveStockAsync(dto.Items, ct);
            if (!reservation.IsSuccess)
            {
                var errorMessages = reservation.Errors.Select(e => 
                    $"{e.ProductName}: {e.Error}");
                throw new InvalidOperationException(string.Join("; ", errorMessages));
            }

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

                    // Create invoice with immutable pricing
                    var inv = new SellingInvoice
                    {
                        CustomerAccountId = dto.CustomerId ?? 0,
                        IssuedAt = dto.InvoiceDate,
                        Currency = dto.Currency,
                        Issuer = "System",
                        Description = dto.Notes
                    };
                    _db.SellingInvoices.Add(inv);
                    await _db.SaveChangesAsync(ct); // get inv.Id

                    // Create invoice lines with snapshot prices
                    foreach (var line in dto.Items)
                    {
                        if (!products.TryGetValue(line.ProductId, out var p))
                            throw new InvalidOperationException($"Product #{line.ProductId} not found.");

                        if (line.Quantity <= 0)
                            throw new InvalidOperationException($"Quantity must be > 0 for {p.Name}.");

                        _db.SellingInvoiceItems.Add(new SellingInvoiceItem
                        {
                            SellingInvoiceId = inv.Id,
                            ProductId = line.ProductId,
                            Quantity = line.Quantity,
                            UnitPrice = line.UnitPrice // Snapshot price at time of sale
                        });
                    }

                    // Update stock quantities
                    var stockUpdates = dto.Items.Select(item => new StockUpdate
                    {
                        ProductId = item.ProductId,
                        QuantityChange = -(int)item.Quantity // Decrement stock
                    }).ToList();
                    
                    await _stockRepository.UpdateStockQuantitiesAsync(stockUpdates, ct);
                    
                    await _accountingService.PostInvoiceAsync(inv, ct);

                    // Release reservation
                    await _stockValidationService.ReleaseStockReservationAsync(reservation.ReservationId, ct);

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
                    // Release reservation on failure
                    try
                    {
                        await _stockValidationService.ReleaseStockReservationAsync(reservation.ReservationId, ct);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "Failed to release stock reservation {ReservationId} on rollback", reservation.ReservationId);
                    }
                    throw;
                }
            }

            throw new InvalidOperationException("Unexpected save flow break.");
        }
    }
}
