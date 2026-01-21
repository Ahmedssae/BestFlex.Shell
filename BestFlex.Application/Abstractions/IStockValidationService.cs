using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using BestFlex.Application.Contracts.Sales;

namespace BestFlex.Application.Abstractions
{
    /// <summary>
    /// Service for validating stock availability and integrity
    /// </summary>
    public interface IStockValidationService
    {
        /// <summary>
        /// Validates stock availability for sale items
        /// </summary>
        /// <param name="items">Items to validate</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>Validation result with details</returns>
        Task<StockValidationResult> ValidateStockAsync(IEnumerable<NewSaleItemDto> items, CancellationToken cancellationToken = default);

        /// <summary>
        /// Reserves stock for a sale transaction
        /// </summary>
        /// <param name="items">Items to reserve</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>Reservation result</returns>
        Task<StockReservationResult> ReserveStockAsync(IEnumerable<NewSaleItemDto> items, CancellationToken cancellationToken = default);

        /// <summary>
        /// Releases reserved stock (used on transaction rollback)
        /// </summary>
        /// <param name="reservationId">Reservation identifier</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>Task representing the operation</returns>
        Task ReleaseStockReservationAsync(string reservationId, CancellationToken cancellationToken = default);
    }

    /// <summary>
    /// Result of stock validation
    /// </summary>
    public class StockValidationResult
    {
        public bool IsValid { get; set; }
        public List<StockValidationError> Errors { get; set; } = new();
        public List<StockWarning> Warnings { get; set; } = new();
    }

    /// <summary>
    /// Stock validation error
    /// </summary>
    public class StockValidationError
    {
        public int ProductId { get; set; }
        public string ProductName { get; set; } = string.Empty;
        public string Error { get; set; } = string.Empty;
        public int RequestedQuantity { get; set; }
        public int AvailableQuantity { get; set; }
    }

    /// <summary>
    /// Stock validation warning
    /// </summary>
    public class StockWarning
    {
        public int ProductId { get; set; }
        public string ProductName { get; set; } = string.Empty;
        public string Warning { get; set; } = string.Empty;
        public int RequestedQuantity { get; set; }
        public int AvailableQuantity { get; set; }
    }

    /// <summary>
    /// Result of stock reservation
    /// </summary>
    public class StockReservationResult
    {
        public bool IsSuccess { get; set; }
        public string ReservationId { get; set; } = string.Empty;
        public List<StockReservationError> Errors { get; set; } = new();
    }

    /// <summary>
    /// Stock reservation error
    /// </summary>
    public class StockReservationError
    {
        public int ProductId { get; set; }
        public string ProductName { get; set; } = string.Empty;
        public string Error { get; set; } = string.Empty;
    }
}
