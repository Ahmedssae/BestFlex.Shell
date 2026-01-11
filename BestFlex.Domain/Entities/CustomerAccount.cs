using System.Collections.Generic;

namespace BestFlex.Domain.Entities
{
    public class CustomerAccount
    {
        public int Id { get; set; }                   // Primary key (auto-increment)
        public string Name { get; set; } = default!;  // Customer name
        public string? Phone { get; set; }            // Optional phone number
        public decimal Balance { get; set; }          // Balance = what they owe or credit

        // Navigation property: list of invoices related to this customer
        public ICollection<SellingInvoice> Invoices { get; set; } = new List<SellingInvoice>();
    }
}
