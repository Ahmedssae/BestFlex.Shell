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

namespace BestFlex.Infrastructure.Services
{
    /// <summary>
    /// Implementation of stock validation service
    /// </summary>
    public class StockValidationService : IStockValidationService
    {
        private readonly IProductRepository _productRepository;
        private readonly IStockRepository _stockRepository;
        private readonly ILogger<StockValidationService> _logger;
        private readonly SemaphoreSlim _validationLock = new(1, 1);

        public StockValidationService(
            IProductRepository productRepository,
            IStockRepository stockRepository,
            ILogger<StockValidationService> logger)
        {
            _productRepository = productRepository ?? throw new ArgumentNullException(nameof(productRepository));
            _stockRepository = stockRepository ?? throw new ArgumentNullException(nameof(stockRepository));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public async Task<StockValidationResult> ValidateStockAsync(IEnumerable<NewSaleItemDto> items, CancellationToken cancellationToken = default)
        {
            if (items == null) throw new ArgumentNullException(nameof(items));

            await _validationLock.WaitAsync(cancellationToken);
            try
            {
                _logger.LogInformation("Starting stock validation for {Count} items", items.Count());

                var result = new StockValidationResult { IsValid = true };
                var productIds = items.Select(i => i.ProductId).Distinct().ToList();

                // Get current stock for all products
                var stockLevels = await _stockRepository.GetByProductIdsAsync(productIds, cancellationToken);
                var products = await _productRepository.GetByIdsAsync(productIds, cancellationToken);

                foreach (var item in items)
                {
                    var stock = stockLevels.FirstOrDefault(s => s.ProductId == item.ProductId);
                    if (!products.TryGetValue(item.ProductId, out var product))
                    {
                        throw new InvalidOperationException($"Product with ID {item.ProductId} not found.");
                    }

                    if (stock == null)
                    {
                        result.IsValid = false;
                        result.Errors.Add(new StockValidationError
                        {
                            ProductId = item.ProductId,
                            ProductName = product.Name,
                            Error = "Product stock not found",
                            RequestedQuantity = (int)item.Quantity,
                            AvailableQuantity = 0
                        });
                        continue;
                    }

                    if (item.Quantity <= 0)
                    {
                        result.IsValid = false;
                        result.Errors.Add(new StockValidationError
                        {
                            ProductId = item.ProductId,
                            ProductName = product.Name,
                            Error = "Quantity must be greater than zero",
                            RequestedQuantity = (int)item.Quantity,
                            AvailableQuantity = stock.Quantity
                        });
                        continue;
                    }

                    if (stock.Quantity < item.Quantity)
                    {
                        result.IsValid = false;
                        result.Errors.Add(new StockValidationError
                        {
                            ProductId = item.ProductId,
                            ProductName = product.Name,
                            Error = "Insufficient stock",
                            RequestedQuantity = (int)item.Quantity,
                            AvailableQuantity = stock.Quantity
                        });
                    }
                    else if (stock.Quantity - item.Quantity < 10) // Low stock warning
                    {
                        result.Warnings.Add(new StockWarning
                        {
                            ProductId = item.ProductId,
                            ProductName = product.Name,
                            Warning = "Low stock warning",
                            RequestedQuantity = (int)item.Quantity,
                            AvailableQuantity = stock.Quantity
                        });
                    }
                }

                _logger.LogInformation("Stock validation completed. Valid: {IsValid}, Errors: {ErrorCount}, Warnings: {WarningCount}", 
                    result.IsValid, result.Errors.Count, result.Warnings.Count);

                return result;
            }
            finally
            {
                _validationLock.Release();
            }
        }

        public async Task<StockReservationResult> ReserveStockAsync(IEnumerable<NewSaleItemDto> items, CancellationToken cancellationToken = default)
        {
            if (items == null) throw new ArgumentNullException(nameof(items));

            await _validationLock.WaitAsync(cancellationToken);
            try
            {
                _logger.LogInformation("Reserving stock for {Count} items", items.Count());

                // First validate stock availability
                var validation = await ValidateStockAsync(items, cancellationToken);
                if (!validation.IsValid)
                {
                    return new StockReservationResult
                    {
                        IsSuccess = false,
                        Errors = validation.Errors.Select(e => new StockReservationError
                        {
                            ProductId = e.ProductId,
                            ProductName = e.ProductName,
                            Error = e.Error
                        }).ToList()
                    };
                }

                // Create reservation
                var reservationId = Guid.NewGuid().ToString("N")[..8]; // Short ID for database
                var reservations = items.Select(item => new StockReservation
                {
                    ReservationId = reservationId,
                    ProductId = item.ProductId,
                    Quantity = (int)item.Quantity,
                    ReservedAt = DateTime.UtcNow,
                    ExpiresAt = DateTime.UtcNow.AddMinutes(30) // 30 minute timeout
                }).ToList();

                await _stockRepository.CreateReservationsAsync(reservations, cancellationToken);

                _logger.LogInformation("Stock reservation {ReservationId} created successfully", reservationId);

                return new StockReservationResult
                {
                    IsSuccess = true,
                    ReservationId = reservationId
                };
            }
            finally
            {
                _validationLock.Release();
            }
        }

        public async Task ReleaseStockReservationAsync(string reservationId, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(reservationId))
                throw new ArgumentException("Reservation ID cannot be null or empty", nameof(reservationId));

            await _validationLock.WaitAsync(cancellationToken);
            try
            {
                _logger.LogInformation("Releasing stock reservation {ReservationId}", reservationId);

                await _stockRepository.DeleteReservationAsync(reservationId, cancellationToken);

                _logger.LogInformation("Stock reservation {ReservationId} released successfully", reservationId);
            }
            finally
            {
                _validationLock.Release();
            }
        }
    }
}
