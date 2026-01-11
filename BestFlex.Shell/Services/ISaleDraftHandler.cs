using System.Threading.Tasks;
using BestFlex.Shell.Models;

namespace BestFlex.Shell.Services
{
    public sealed class SaveResult
    {
        public bool Success { get; set; }
        public long? InvoiceId { get; set; }
        public string? Message { get; set; }
        public static SaveResult Ok(long? id, string? msg = null) => new() { Success = true, InvoiceId = id, Message = msg };
        public static SaveResult Fail(string msg) => new() { Success = false, Message = msg };
    }

    public interface ISaleDraftHandler
    {
        Task<SaveResult> SaveAsync(SaleDraft draft);
    }
}
