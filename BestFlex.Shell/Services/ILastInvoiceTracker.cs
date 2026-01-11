namespace BestFlex.Shell.Services
{
    public interface ILastInvoiceTracker
    {
        int? LastInvoiceId { get; set; }
        System.DateTimeOffset? When { get; set; }
    }
}
