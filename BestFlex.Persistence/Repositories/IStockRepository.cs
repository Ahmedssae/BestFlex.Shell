using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using BestFlex.Domain.Entities;

namespace BestFlex.Persistence.Repositories
{
    /// <summary>
    /// Repository for stock operations
    /// </summary>
    public interface IStockRepository
    {
        /// <summary>
        /// Gets stock levels for specified products
        /// </summary>
        /// <param name="productIds">Product IDs to query</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>Stock entities</returns>
        Task<List<Stock>> GetByProductIdsAsync(IEnumerable<int> productIds, CancellationToken cancellationToken = default);

        /// <summary>
        /// Creates stock reservations
        /// </summary>
        /// <param name="reservations">Reservations to create</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>Task representing operation</returns>
        Task CreateReservationsAsync(IEnumerable<StockReservation> reservations, CancellationToken cancellationToken = default);

        /// <summary>
        /// Deletes a stock reservation
        /// </summary>
        /// <param name="reservationId">Reservation identifier</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>Task representing operation</returns>
        Task DeleteReservationAsync(string reservationId, CancellationToken cancellationToken = default);

        /// <summary>
        /// Updates stock quantities (used during sale completion)
        /// </summary>
        /// <param name="stockUpdates">Stock updates to apply</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>Task representing operation</returns>
        Task UpdateStockQuantitiesAsync(IEnumerable<StockUpdate> stockUpdates, CancellationToken cancellationToken = default);

        /// <summary>
        /// Gets expired reservations for cleanup
        /// </summary>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>Expired reservations</returns>
        Task<List<StockReservation>> GetExpiredReservationsAsync(CancellationToken cancellationToken = default);

        /// <summary>
        /// Deletes expired reservations
        /// </summary>
        /// <param name="reservationIds">IDs of expired reservations</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>Task representing operation</returns>
        Task DeleteExpiredReservationsAsync(IEnumerable<string> reservationIds, CancellationToken cancellationToken = default);
    }

    /// <summary>
    /// Stock update operation
    /// </summary>
    public class StockUpdate
    {
        public int ProductId { get; set; }
        public int QuantityChange { get; set; } // Negative for decrement
    }
}
