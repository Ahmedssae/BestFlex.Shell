namespace BestFlex.Application.UI
{
    /// <summary>
    /// Summary data for an invoice in the UI
    /// </summary>
    public class InvoiceSummaryDto
    {
        public int InvoiceId { get; set; }
        public string InvoiceNo { get; set; } = string.Empty;
        public DateTime IssuedAt { get; set; }
        public decimal Amount { get; set; }
        public string Currency { get; set; } = "USD";
    }
}
