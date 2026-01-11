using System;
using System.Collections.Generic;

namespace BestFlex.Application.Contracts.Sales
{
    public sealed class NewSaleDto
    {
        public int? CustomerId { get; set; }           // optional walk-in
        public DateTime InvoiceDate { get; set; } = DateTime.UtcNow;
        public string Currency { get; set; } = "USD";
        public string? Notes { get; set; }
        public List<NewSaleItemDto> Items { get; set; } = new();
    }

    public sealed class NewSaleItemDto
    {
        public int ProductId { get; set; }
        public decimal Quantity { get; set; }
        public decimal UnitPrice { get; set; }         // final price used
    }
}
