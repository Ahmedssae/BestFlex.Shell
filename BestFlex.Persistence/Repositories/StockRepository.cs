using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using BestFlex.Domain.Entities;
using BestFlex.Persistence.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace BestFlex.Persistence.Repositories
{
    /// <summary>
    /// Implementation of stock repository
    /// </summary>
    public class StockRepository : IStockRepository
    {
        private readonly BestFlexDbContext _db;
        private readonly ILogger<StockRepository> _logger;

        public StockRepository(BestFlexDbContext db, ILogger<StockRepository> logger)
        {
            _db = db ?? throw new ArgumentNullException(nameof(db));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public async Task<List<Stock>> GetByProductIdsAsync(IEnumerable<int> productIds, CancellationToken cancellationToken = default)
        {
            return await _db.Stocks
                .Where(s => productIds.Contains(s.ProductId))
                .ToListAsync(cancellationToken);
        }

        public async Task CreateReservationsAsync(IEnumerable<StockReservation> reservations, CancellationToken cancellationToken = default)
        {
            if (reservations == null) throw new ArgumentNullException(nameof(reservations));

            _db.StockReservations.AddRange(reservations);
            await _db.SaveChangesAsync(cancellationToken);
            
            _logger.LogInformation("Created {Count} stock reservations", reservations.Count());
        }

        public async Task DeleteReservationAsync(string reservationId, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(reservationId))
                throw new ArgumentException("Reservation ID cannot be null or empty", nameof(reservationId));

            var reservation = await _db.StockReservations
                .FirstOrDefaultAsync(r => r.ReservationId == reservationId, cancellationToken);

            if (reservation != null)
            {
                _db.StockReservations.Remove(reservation);
                await _db.SaveChangesAsync(cancellationToken);
                
                _logger.LogInformation("Deleted stock reservation {ReservationId}", reservationId);
            }
        }

        public async Task UpdateStockQuantitiesAsync(IEnumerable<StockUpdate> stockUpdates, CancellationToken cancellationToken = default)
        {
            if (stockUpdates == null) throw new ArgumentNullException(nameof(stockUpdates));

            var productIds = stockUpdates.Select(u => u.ProductId).ToList();
            var stocks = await _db.Stocks
                .Where(s => productIds.Contains(s.ProductId))
                .ToDictionaryAsync(s => s.ProductId, cancellationToken);

            foreach (var update in stockUpdates)
            {
                if (stocks.TryGetValue(update.ProductId, out var stock))
                {
                    stock.Quantity += update.QuantityChange; // QuantityChange is negative for decrements
                    stock.UpdatedAt = DateTime.UtcNow;
                }
            }

            await _db.SaveChangesAsync(cancellationToken);
            
            _logger.LogInformation("Updated stock quantities for {Count} products", stockUpdates.Count());
        }

        public async Task<List<StockReservation>> GetExpiredReservationsAsync(CancellationToken cancellationToken = default)
        {
            var expiredTime = DateTime.UtcNow;
            return await _db.StockReservations
                .Where(r => r.ExpiresAt <= expiredTime)
                .ToListAsync(cancellationToken);
        }

        public async Task DeleteExpiredReservationsAsync(IEnumerable<string> reservationIds, CancellationToken cancellationToken = default)
        {
            if (reservationIds == null) throw new ArgumentNullException(nameof(reservationIds));

            var expiredReservations = await _db.StockReservations
                .Where(r => reservationIds.Contains(r.ReservationId))
                .ToListAsync(cancellationToken);

            if (expiredReservations.Any())
            {
                _db.StockReservations.RemoveRange(expiredReservations);
                await _db.SaveChangesAsync(cancellationToken);
                
                _logger.LogInformation("Deleted {Count} expired stock reservations", expiredReservations.Count());
            }
        }
    }
}
