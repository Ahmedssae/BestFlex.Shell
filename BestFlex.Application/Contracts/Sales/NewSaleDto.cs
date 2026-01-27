using System;
using System.Collections.Generic;
using BestFlex.Application.Abstractions.Contracts.Sales;

namespace BestFlex.Application.Contracts.Sales
{
    public sealed class NewSaleDto
    {
        public int? CustomerId { get; set; }           // optional walk-in
        public DateTime InvoiceDate { get; set; } = DateTime.UtcNow;
        public string Currency { get; set; } = "USD";
        public string? Notes { get; set; }
        public List<NewSaleItemDto> Items { get; set; } = new();

        // Implicit conversion to the Abstractions version
        public static implicit operator BestFlex.Application.Abstractions.Contracts.Sales.NewSaleDto(NewSaleDto dto)
        {
            if (dto == null) return null;
            return new BestFlex.Application.Abstractions.Contracts.Sales.NewSaleDto
            {
                CustomerId = dto.CustomerId,
                InvoiceDate = dto.InvoiceDate,
                Currency = dto.Currency,
                Notes = dto.Notes,
                Items = dto.Items.Select(item => (BestFlex.Application.Abstractions.Contracts.Sales.NewSaleItemDto)item).ToList()
            };
        }
    }

    public sealed class NewSaleItemDto
    {
        public int ProductId { get; set; }
        public decimal Quantity { get; set; }
        public decimal UnitPrice { get; set; }         // final price used

        // Implicit conversion to the Abstractions version
        public static implicit operator BestFlex.Application.Abstractions.Contracts.Sales.NewSaleItemDto(NewSaleItemDto item)
        {
            if (item == null) return null;
            return new BestFlex.Application.Abstractions.Contracts.Sales.NewSaleItemDto
            {
                ProductId = item.ProductId,
                Quantity = item.Quantity,
                UnitPrice = item.UnitPrice
            };
        }
    }
}
