namespace BestFlex.Domain.Entities
{
    public sealed class PrintTemplate : EntityBase
    {
        public int CompanyId { get; set; }
        public Company Company { get; set; } = default!;

        // “invoice” for now (room for receipts/quotes later)
        public string DocType { get; set; } = "invoice";

        // “FlowDocument” initially (future: QuestPDF, etc.)
        public string Engine { get; set; } = "FlowDocument";

        // XAML payload (or JSON later)
        public string Payload { get; set; } = "";

        // Whether this is the default template for the doc type
        public bool IsDefault { get; set; }
    }
}
