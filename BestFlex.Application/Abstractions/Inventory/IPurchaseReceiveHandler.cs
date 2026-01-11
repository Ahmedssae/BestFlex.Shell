using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace BestFlex.Application.Abstractions.Inventory
{
    public sealed record ReceiveLine(string Code, string? Name, decimal Quantity, decimal UnitCost);
    public sealed record ReceiveDraft(
        string Supplier,
        string DocumentNumber,
        DateTime Date,
        IReadOnlyList<ReceiveLine> Lines,
        string? Notes
    );

    public sealed record PurchaseReceiptResult(Guid ReceiptId, string DocumentNumber, DateTime Date);

    public interface IPurchaseReceiveHandler
    {
        /// <summary>
        /// Persist a received stock document and increment stock levels.
        /// Returns the created receipt identity for printing.
        /// </summary>
        Task<PurchaseReceiptResult> ReceiveAsync(ReceiveDraft draft, CancellationToken ct = default);
    }
}
