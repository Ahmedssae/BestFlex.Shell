using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace BestFlex.Shell.Services
{
    public interface IAuditTrailService
    {
        Task<string> LogPostingAsync(int salesOrderId, int invoiceId, string invoiceNumber, string userName);
        Task<string> LogValidationFailureAsync(int salesOrderId, string reason, string userName);
        Task<List<AuditEntry>> GetAuditTrailAsync(int salesOrderId);
    }

    public class AuditEntry
    {
        public int Id { get; set; }
        public int SalesOrderId { get; set; }
        public string Action { get; set; } = string.Empty;
        public string Details { get; set; } = string.Empty;
        public string UserName { get; set; } = string.Empty;
        public DateTime Timestamp { get; set; }
        public string TransactionId { get; set; } = string.Empty;
    }
}
