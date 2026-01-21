using System;

namespace BestFlex.Domain.Entities
{
    /// <summary>
    /// Represents stock quantity for a product
    /// </summary>
    public class Stock
    {
        public int Id { get; set; }
        
        public int ProductId { get; set; }
        
        /// <summary>
        /// Current quantity available for sale
        /// </summary>
        public int Quantity { get; set; }
        
        /// <summary>
        /// When stock was last updated
        /// </summary>
        public DateTime UpdatedAt { get; set; }
        
        /// <summary>
        /// Navigation property to the product
        /// </summary>
        public Product Product { get; set; } = null!;
    }
}
