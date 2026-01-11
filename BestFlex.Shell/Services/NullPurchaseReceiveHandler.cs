using System;
using System.Threading;
using System.Threading.Tasks;
using BestFlex.Application.Abstractions.Inventory;

namespace BestFlex.Shell.Services
{
    /// <summary>
    /// Drop-in handler that completes instantly.
    /// It returns a receipt identity so the GRN can be printed.
    /// Replace with an EF-backed implementation when wiring DB later.
    /// </summary>
    public sealed class NullPurchaseReceiveHandler : IPurchaseReceiveHandler
    {
        public Task<PurchaseReceiptResult> ReceiveAsync(ReceiveDraft draft, CancellationToken ct = default)
        {
            if (draft is null) throw new ArgumentNullException(nameof(draft));
            var id = Guid.NewGuid();
            var res = new PurchaseReceiptResult(id, draft.DocumentNumber, draft.Date);
            return Task.FromResult(res);
        }
    }
}
