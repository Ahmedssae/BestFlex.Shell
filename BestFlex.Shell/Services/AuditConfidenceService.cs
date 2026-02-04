using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using BestFlex.Application.Abstractions;

namespace BestFlex.Shell.Services
{
    /// <summary>
    /// Provides audit confidence signals with transaction IDs and comprehensive logging
    /// </summary>
    public class AuditConfidenceService : IAuditConfidenceService
    {
        private readonly ILogger<AuditConfidenceService> _logger;
        private readonly ICurrentUserService _currentUserService;
        private readonly ConcurrentDictionary<string, AuditTransaction> _activeTransactions = new();

        public AuditConfidenceService(
            ILogger<AuditConfidenceService> logger,
            ICurrentUserService currentUserService)
        {
            _logger = logger;
            _currentUserService = currentUserService;
        }

        public string StartTransaction(string operationType, string? description = null, Dictionary<string, object>? context = null)
        {
            var transactionId = GenerateTransactionId();
            var username = _currentUserService.Username ?? "<unknown>";

            var transaction = new AuditTransaction
            {
                TransactionId = transactionId,
                OperationType = operationType,
                Description = description ?? operationType,
                Username = username,
                StartTime = DateTime.UtcNow,
                Status = TransactionStatus.Started,
                Context = context ?? new Dictionary<string, object>()
            };

            _activeTransactions.TryAdd(transactionId, transaction);

            _logger.LogInformation("[AUDIT_TRANSACTION_START] [TX:{TransactionId}] [User:{Username}] [Operation:{OperationType}] {Description} {Context}", 
                transactionId, username, operationType, description ?? "", 
                context != null ? $"Context: {string.Join(", ", context.Select(kvp => $"{kvp.Key}={kvp.Value}"))}" : "");

            return transactionId;
        }

        public void CompleteTransaction(string transactionId, bool success, string? result = null, Dictionary<string, object>? resultContext = null)
        {
            if (_activeTransactions.TryRemove(transactionId, out var transaction))
            {
                transaction.EndTime = DateTime.UtcNow;
                transaction.Duration = transaction.EndTime.HasValue ? transaction.EndTime.Value - transaction.StartTime : TimeSpan.Zero;
                transaction.Status = success ? TransactionStatus.Completed : TransactionStatus.Failed;
                transaction.Result = result;
                transaction.ResultContext = resultContext ?? new Dictionary<string, object>();

                var logLevel = success ? LogLevel.Information : LogLevel.Warning;
                _logger.Log(logLevel, "[AUDIT_TRANSACTION_END] [TX:{TransactionId}] [User:{Username}] [Operation:{OperationType}] [Status:{Status}] {Result} Duration: {Duration}ms {ResultContext}", 
                    transactionId, transaction.Username, transaction.OperationType, transaction.Status, 
                    result ?? "", transaction.Duration.TotalMilliseconds,
                    resultContext != null ? $"ResultContext: {string.Join(", ", resultContext.Select(kvp => $"{kvp.Key}={kvp.Value}"))}" : "");
            }
            else
            {
                _logger.LogWarning("[AUDIT_TRANSACTION_NOT_FOUND] [TX:{TransactionId}] Attempted to complete non-existent transaction", transactionId);
            }
        }

        public void LogFinancialAction(string transactionId, string action, decimal amount, string? account = null, string? reference = null)
        {
            var username = _currentUserService.Username ?? "<unknown>";

            _logger.LogInformation("[AUDIT_FINANCIAL] [TX:{TransactionId}] [User:{Username}] [Action:{Action}] [Amount:{Amount:C}] [Account:{Account}] [Reference:{Reference}]", 
                transactionId, username, action, amount, account ?? "<unknown>", reference ?? "<unknown>");

            // Also log to the transaction if it exists
            if (_activeTransactions.TryGetValue(transactionId, out var transaction))
            {
                transaction.FinancialActions.Add(new FinancialAction
                {
                    Action = action,
                    Amount = amount,
                    Account = account,
                    Reference = reference,
                    Timestamp = DateTime.UtcNow
                });
            }
        }

        public void LogDataAccess(string transactionId, string operation, string tableName, int recordCount, bool success)
        {
            var username = _currentUserService.Username ?? "<unknown>";

            _logger.LogInformation("[AUDIT_DATA_ACCESS] [TX:{TransactionId}] [User:{Username}] [Operation:{Operation}] [Table:{TableName}] [Records:{RecordCount}] [Success:{Success}]", 
                transactionId, username, operation, tableName, recordCount, success);

            // Also log to the transaction if it exists
            if (_activeTransactions.TryGetValue(transactionId, out var transaction))
            {
                transaction.DataActions.Add(new DataAction
                {
                    Operation = operation,
                    TableName = tableName,
                    RecordCount = recordCount,
                    Success = success,
                    Timestamp = DateTime.UtcNow
                });
            }
        }

        public void LogSecurityEvent(string transactionId, string eventType, string resource, string? details = null)
        {
            var username = _currentUserService.Username ?? "<unknown>";

            _logger.LogInformation("[AUDIT_SECURITY] [TX:{TransactionId}] [User:{Username}] [EventType:{EventType}] [Resource:{Resource}] {Details}", 
                transactionId, username, eventType, resource, details ?? "");

            // Also log to the transaction if it exists
            if (_activeTransactions.TryGetValue(transactionId, out var transaction))
            {
                transaction.SecurityEvents.Add(new SecurityEvent
                {
                    EventType = eventType,
                    Resource = resource,
                    Details = details,
                    Timestamp = DateTime.UtcNow
                });
            }
        }

        public AuditTransaction? GetTransaction(string transactionId)
        {
            return _activeTransactions.TryGetValue(transactionId, out var transaction) ? transaction : null;
        }

        public AuditTransaction[] GetActiveTransactions()
        {
            return _activeTransactions.Values.ToArray();
        }

        public AuditSummary GetAuditSummary(DateTime? startDate = null, DateTime? endDate = null)
        {
            var start = startDate ?? DateTime.UtcNow.AddDays(-7); // Default to last 7 days
            var end = endDate ?? DateTime.UtcNow;

            // In a real implementation, this would query the audit log database
            // For now, return a summary based on active transactions
            var activeCount = _activeTransactions.Count;
            var completedCount = _activeTransactions.Values.Count(t => t.Status == TransactionStatus.Completed);
            var failedCount = _activeTransactions.Values.Count(t => t.Status == TransactionStatus.Failed);

            return new AuditSummary
            {
                StartDate = start,
                EndDate = end,
                TotalTransactions = activeCount + completedCount + failedCount,
                ActiveTransactions = activeCount,
                CompletedTransactions = completedCount,
                FailedTransactions = failedCount,
                SuccessRate = activeCount + completedCount + failedCount > 0 ? 
                    (double)(completedCount) / (activeCount + completedCount + failedCount) : 0
            };
        }

        public string GenerateAuditReport(DateTime startDate, DateTime endDate)
        {
            var summary = GetAuditSummary(startDate, endDate);
            
            var report = $@"
AUDIT REPORT
=============
Period: {startDate:yyyy-MM-dd} to {endDate:yyyy-MM-dd}

SUMMARY:
- Total Transactions: {summary.TotalTransactions}
- Active: {summary.ActiveTransactions}
- Completed: {summary.CompletedTransactions}
- Failed: {summary.FailedTransactions}
- Success Rate: {summary.SuccessRate:P1}

ACTIVE TRANSACTIONS:
{string.Join("\n", _activeTransactions.Values.Select(t => $"- {t.TransactionId}: {t.OperationType} ({t.Status})"))}

Generated: {DateTime.UtcNow:yyyy-MM-dd HH:mm:ss} UTC
";

            return report;
        }

        public void EnsureAuditIntegrity()
        {
            // Verify that all financial actions have proper transaction IDs
            foreach (var transaction in _activeTransactions.Values)
            {
                if (transaction.FinancialActions.Count > 0 && string.IsNullOrEmpty(transaction.TransactionId))
                {
                    _logger.LogError("[AUDIT_INTEGRITY] [User:{Username}] Financial actions found without transaction ID", transaction.Username);
                }
            }

            // Check for orphaned transactions (active for too long)
            var now = DateTime.UtcNow;
            foreach (var transaction in _activeTransactions.Values)
            {
                var activeTime = now - transaction.StartTime;
                if (activeTime.TotalHours > 2) // Transactions active for more than 2 hours
                {
                    _logger.LogWarning("[AUDIT_ORPHANED] [TX:{TransactionId}] [User:{Username}] Transaction active for {Duration} hours", 
                        transaction.TransactionId, transaction.Username, activeTime.TotalHours);
                }
            }
        }

        private string GenerateTransactionId()
        {
            var guidStr = Guid.NewGuid().ToString("N");
            return $"TX-{DateTime.UtcNow:yyyyMMdd-HHmmss}-{guidStr.Substring(0, 8).ToUpper()}";
        }
    }

    public interface IAuditConfidenceService
    {
        string StartTransaction(string operationType, string? description = null, Dictionary<string, object>? context = null);
        void CompleteTransaction(string transactionId, bool success, string? result = null, Dictionary<string, object>? resultContext = null);
        void LogFinancialAction(string transactionId, string action, decimal amount, string? account = null, string? reference = null);
        void LogDataAccess(string transactionId, string operation, string tableName, int recordCount, bool success);
        void LogSecurityEvent(string transactionId, string eventType, string resource, string? details = null);
        AuditTransaction? GetTransaction(string transactionId);
        AuditTransaction[] GetActiveTransactions();
        AuditSummary GetAuditSummary(DateTime? startDate = null, DateTime? endDate = null);
        string GenerateAuditReport(DateTime startDate, DateTime endDate);
        void EnsureAuditIntegrity();
    }

    public class AuditTransaction
    {
        public string TransactionId { get; set; } = string.Empty;
        public string OperationType { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public string Username { get; set; } = string.Empty;
        public DateTime StartTime { get; set; }
        public DateTime? EndTime { get; set; }
        public TimeSpan Duration { get; set; }
        public TransactionStatus Status { get; set; }
        public string? Result { get; set; }
        public Dictionary<string, object> Context { get; set; } = new();
        public Dictionary<string, object> ResultContext { get; set; } = new();
        public List<FinancialAction> FinancialActions { get; set; } = new();
        public List<DataAction> DataActions { get; set; } = new();
        public List<SecurityEvent> SecurityEvents { get; set; } = new();
    }

    public class FinancialAction
    {
        public string Action { get; set; } = string.Empty;
        public decimal Amount { get; set; }
        public string? Account { get; set; }
        public string? Reference { get; set; }
        public DateTime Timestamp { get; set; }
    }

    public class DataAction
    {
        public string Operation { get; set; } = string.Empty;
        public string TableName { get; set; } = string.Empty;
        public int RecordCount { get; set; }
        public bool Success { get; set; }
        public DateTime Timestamp { get; set; }
    }

    public class SecurityEvent
    {
        public string EventType { get; set; } = string.Empty;
        public string Resource { get; set; } = string.Empty;
        public string? Details { get; set; }
        public DateTime Timestamp { get; set; }
    }

    public class AuditSummary
    {
        public DateTime StartDate { get; set; }
        public DateTime EndDate { get; set; }
        public int TotalTransactions { get; set; }
        public int ActiveTransactions { get; set; }
        public int CompletedTransactions { get; set; }
        public int FailedTransactions { get; set; }
        public double SuccessRate { get; set; }
    }

    public enum TransactionStatus
    {
        Started,
        Completed,
        Failed,
        Cancelled
    }
}
