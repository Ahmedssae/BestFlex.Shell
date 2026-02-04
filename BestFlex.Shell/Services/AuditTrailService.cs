using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace BestFlex.Shell.Services
{
    public class AuditTrailService : IAuditTrailService
    {
        private readonly ILogger<AuditTrailService> _logger;
        private readonly List<AuditEntry> _auditLog = new();

        public AuditTrailService(ILogger<AuditTrailService> logger)
        {
            _logger = logger;
        }

        public async Task<string> LogPostingAsync(int salesOrderId, int invoiceId, string invoiceNumber, string userName)
        {
            var transactionId = Guid.NewGuid().ToString("N")[..8].ToUpper();
            
            var entry = new AuditEntry
            {
                SalesOrderId = salesOrderId,
                Action = "POSTED",
                Details = $"Sales Order {salesOrderId} posted as Invoice {invoiceNumber} (ID: {invoiceId})",
                UserName = userName,
                Timestamp = DateTime.UtcNow,
                TransactionId = transactionId
            };

            _auditLog.Add(entry);
            
            _logger.LogInformation("AUDIT: [{TransactionId}] {UserName} posted Sales Order {SalesOrderId} as Invoice {InvoiceNumber}", 
                transactionId, userName, salesOrderId, invoiceNumber);

            await Task.CompletedTask;
            return transactionId;
        }

        public async Task<string> LogValidationFailureAsync(int salesOrderId, string reason, string userName)
        {
            var transactionId = Guid.NewGuid().ToString("N")[..8].ToUpper();
            
            var entry = new AuditEntry
            {
                SalesOrderId = salesOrderId,
                Action = "VALIDATION_FAILED",
                Details = $"Posting failed: {reason}",
                UserName = userName,
                Timestamp = DateTime.UtcNow,
                TransactionId = transactionId
            };

            _auditLog.Add(entry);
            
            _logger.LogWarning("AUDIT: [{TransactionId}] {UserName} failed to post Sales Order {SalesOrderId}: {Reason}", 
                transactionId, userName, salesOrderId, reason);

            await Task.CompletedTask;
            return transactionId;
        }

        public async Task<List<AuditEntry>> GetAuditTrailAsync(int salesOrderId)
        {
            var entries = _auditLog
                .Where(e => e.SalesOrderId == salesOrderId)
                .OrderByDescending(e => e.Timestamp)
                .ToList();

            await Task.CompletedTask;
            return entries;
        }
    }
}
