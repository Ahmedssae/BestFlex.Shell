using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace BestFlex.Shell.Models
{
    // Domain entity for database persistence
    [Table("SalesOrders")]
    public class SalesOrder
    {
        [Key]
        public int Id { get; set; }

        [Required]
        [StringLength(50)]
        public string OrderNumber { get; set; } = string.Empty;

        [Required]
        [StringLength(200)]
        public string CustomerName { get; set; } = string.Empty;

        [Required]
        public DateTime OrderDate { get; set; }

        [Required]
        [StringLength(10)]
        public string Currency { get; set; } = "USD";

        [Column(TypeName = "decimal(18,2)")]
        public decimal Subtotal { get; set; }

        [Column(TypeName = "decimal(18,2)")]
        public decimal Tax { get; set; }

        [Column(TypeName = "decimal(18,2)")]
        public decimal GrandTotal { get; set; }

        [Required]
        public DateTime CreatedAt { get; set; }

        public DateTime? UpdatedAt { get; set; }

        // Navigation property
        public virtual ICollection<SalesOrderLine> Lines { get; set; } = new List<SalesOrderLine>();
    }

    // Domain entity for order lines
    [Table("SalesOrderLines")]
    public class SalesOrderLine
    {
        [Key]
        public int Id { get; set; }

        [Required]
        public int SalesOrderId { get; set; }

        [Required]
        [StringLength(500)]
        public string Description { get; set; } = string.Empty;

        [Required]
        [Column(TypeName = "decimal(18,4)")]
        public decimal Quantity { get; set; }

        [Required]
        [Column(TypeName = "decimal(18,2)")]
        public decimal UnitPrice { get; set; }

        [Required]
        [Column(TypeName = "decimal(18,2)")]
        public decimal LineTotal { get; set; }

        [Required]
        public DateTime CreatedAt { get; set; }

        public DateTime? UpdatedAt { get; set; }

        // Navigation property back to parent
        [ForeignKey("SalesOrderId")]
        public virtual SalesOrder SalesOrder { get; set; } = null!;
    }
}
