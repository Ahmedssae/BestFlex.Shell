namespace BestFlex.Domain.Entities
{
    public class SellingInvoiceItem
    {
        public int Id { get; set; }                    // Primary key

        // Foreign key to parent invoice
        public int SellingInvoiceId { get; set; }
        public SellingInvoice SellingInvoice { get; set; } = default!;

        // Foreign key to the product being sold
        public int ProductId { get; set; }
        public Product Product { get; set; } = default!;

        public decimal Quantity { get; set; }          // How many pieces
        public decimal UnitPrice { get; set; }         // Price per unit

        // Computed (not stored in DB)
        public decimal LineTotal => Quantity * UnitPrice;
    }
}
