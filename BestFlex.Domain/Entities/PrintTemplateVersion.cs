namespace BestFlex.Domain.Entities
{
    public sealed class PrintTemplateVersion : EntityBase
    {
        public int CompanyId { get; set; }
        public Company Company { get; set; } = default!;

        public string DocType { get; set; } = "invoice";
        public string Engine { get; set; } = "FlowDocument";
        public string Payload { get; set; } = "";

        public bool IsDefault { get; set; }

        // Audit
        public DateTime CreatedAtUtc { get; set; }
        public string? CreatedBy { get; set; }
    }
}
