using System;

namespace BestFlex.Domain.Entities
{
    /// <summary>
    /// Represents a stock reservation for a sale transaction
    /// </summary>
    public class StockReservation
    {
        public int Id { get; set; }
        
        /// <summary>
        /// Unique identifier for the reservation group
        /// </summary>
        public string ReservationId { get; set; } = string.Empty;
        
        public int ProductId { get; set; }
        
        /// <summary>
        /// Quantity reserved for this product
        /// </summary>
        public int Quantity { get; set; }
        
        /// <summary>
        /// When the reservation was created
        /// </summary>
        public DateTime ReservedAt { get; set; }
        
        /// <summary>
        /// When the reservation expires (auto-cleanup)
        /// </summary>
        public DateTime ExpiresAt { get; set; }
        
        /// <summary>
        /// Navigation property to the product
        /// </summary>
        public Product Product { get; set; } = null!;
    }
}
