using System;

namespace BestFlex.Shell.Services
{
    // Simple DTO for draft lines (matches our ViewModel structure)
    public class SalesOrderLineDraft
    {
        public string Description { get; set; } = string.Empty;
        public decimal Quantity { get; set; }
        public decimal UnitPrice { get; set; }
        public decimal LineTotal => Quantity * UnitPrice;
    }
}
