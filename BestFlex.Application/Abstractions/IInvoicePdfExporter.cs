namespace BestFlex.Application.Abstractions;

public interface IInvoicePdfExporter
{
    Task<byte[]> RenderPdfAsync(InvoicePrintData data, CancellationToken ct = default);
}
