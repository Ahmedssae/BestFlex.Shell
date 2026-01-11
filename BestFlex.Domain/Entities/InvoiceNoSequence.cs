namespace BestFlex.Domain.Entities
{
    public sealed class InvoiceNoSequence : EntityBase
    {
        public int CompanyId { get; set; }
        public string YearMonth { get; set; } = ""; // "yyyyMM"
        public int Next { get; set; }               // next number to allocate
        public byte[] RowVersion { get; set; } = default!; // concurrency token
    }
}
