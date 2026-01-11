using System.Threading.Tasks;
using BestFlex.Shell.Models;

namespace BestFlex.Shell.Services
{
    /// <summary>
    /// Temporary implementation: simulates a save so the page flow works.
    /// When your SellingService/InvoiceRepository files arrive, we’ll replace this with a real handler.
    /// </summary>
    public sealed class NoOpSaleDraftHandler : ISaleDraftHandler
    {
        public Task<SaveResult> SaveAsync(SaleDraft draft)
        {
            // You can log or validate here if needed.
            // Simulate an invoice ID:
            var fakeId = (long)System.DateTime.Now.Ticks % 1_000_000;
            return Task.FromResult(SaveResult.Ok(fakeId, "Saved locally (placeholder)."));
        }
    }
}
