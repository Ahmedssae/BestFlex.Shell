using System;
using System.Collections.Generic;

namespace BestFlex.Domain.Entities
{
    public class SellingInvoice
    {
        public int Id { get; set; }                   // Primary key
        public string InvoiceNo { get; set; } = default!;  // e.g. "INV-0001"
        public DateTime IssuedAt { get; set; }        // Date/time of sale
        public string Currency { get; set; } = "USD"; // Currency used
        public string Issuer { get; set; } = default!; // Who issued (logged-in user)
        public string? Description { get; set; }      // Optional notes (e.g. “Cash payment”)

        // Foreign key to the customer who owns this invoice
        public int CustomerAccountId { get; set; }
        public CustomerAccount CustomerAccount { get; set; } = default!;

        // Navigation property: items inside the invoice
        public ICollection<SellingInvoiceItem> Items { get; set; } = new List<SellingInvoiceItem>();
    }
}
