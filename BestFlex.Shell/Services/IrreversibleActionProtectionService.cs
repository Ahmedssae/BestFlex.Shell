using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using BestFlex.Application.Abstractions;

namespace BestFlex.Shell.Services
{
    /// <summary>
    /// Provides hardened protection for irreversible actions with comprehensive confirmation
    /// </summary>
    public class IrreversibleActionProtectionService : IIrreversibleActionProtectionService
    {
        private readonly ILogger<IrreversibleActionProtectionService> _logger;
        private readonly ICurrentUserService _currentUserService;
        private readonly IConfirmationDialogService _confirmationDialog;
        private readonly IAuditConfidenceService _auditService;

        public IrreversibleActionProtectionService(
            ILogger<IrreversibleActionProtectionService> logger,
            ICurrentUserService currentUserService,
            IConfirmationDialogService confirmationDialog,
            IAuditConfidenceService auditService)
        {
            _logger = logger;
            _currentUserService = currentUserService;
            _confirmationDialog = confirmationDialog;
            _auditService = auditService;
        }

        public async Task<bool> ConfirmInvoicePostingAsync(InvoicePostingContext context)
        {
            var transactionId = _auditService.StartTransaction("InvoicePosting", 
                $"Posting invoice {context.InvoiceNumber} for {context.CustomerName}", 
                new Dictionary<string, object>
                {
                    ["InvoiceNumber"] = context.InvoiceNumber,
                    ["CustomerId"] = context.CustomerId,
                    ["TotalAmount"] = context.TotalAmount,
                    ["LineCount"] = context.LineCount
                });

            try
            {
                // Build comprehensive impact summary
                var impact = BuildInvoicePostingImpact(context);
                
                _logger.LogInformation("[INVOICE_POSTING_CONFIRM] [TX:{TransactionId}] [User:{User}] Requesting confirmation for invoice posting", 
                    transactionId, _currentUserService.Username);

                var confirmed = await _confirmationDialog.ConfirmInvoicePostingAsync(
                    context.InvoiceNumber, 
                    context.TotalAmount);

                if (confirmed)
                {
                    _auditService.LogFinancialAction(transactionId, "InvoicePosted", context.TotalAmount, 
                        $"Customer:{context.CustomerId}", context.InvoiceNumber);
                }

                _auditService.CompleteTransaction(transactionId, confirmed, 
                    confirmed ? "Invoice posting confirmed by user" : "Invoice posting cancelled by user");

                return confirmed;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[INVOICE_POSTING_CONFIRM_ERROR] [TX:{TransactionId}] Error during invoice posting confirmation", 
                    transactionId);
                _auditService.CompleteTransaction(transactionId, false, $"Error: {ex.Message}");
                return false;
            }
        }

        public async Task<bool> ConfirmPeriodClosingAsync(PeriodClosingContext context)
        {
            var transactionId = _auditService.StartTransaction("PeriodClosing", 
                $"Closing accounting period {context.PeriodName}", 
                new Dictionary<string, object>
                {
                    ["PeriodId"] = context.PeriodId,
                    ["PeriodName"] = context.PeriodName,
                    ["EndDate"] = context.EndDate,
                    ["TransactionCount"] = context.TransactionCount
                });

            try
            {
                var impact = BuildPeriodClosingImpact(context);
                
                _logger.LogInformation("[PERIOD_CLOSING_CONFIRM] [TX:{TransactionId}] [User:{User}] Requesting confirmation for period closing", 
                    transactionId, _currentUserService.Username);

                var confirmed = await _confirmationDialog.ConfirmPeriodClosingAsync(
                    context.PeriodName, 
                    context.EndDate);

                if (confirmed)
                {
                    _auditService.LogSecurityEvent(transactionId, "PeriodClosed", 
                        $"Period:{context.PeriodId}", $"Closed by {_currentUserService.Username}");
                }

                _auditService.CompleteTransaction(transactionId, confirmed, 
                    confirmed ? "Period closing confirmed by user" : "Period closing cancelled by user");

                return confirmed;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[PERIOD_CLOSING_CONFIRM_ERROR] [TX:{TransactionId}] Error during period closing confirmation", 
                    transactionId);
                _auditService.CompleteTransaction(transactionId, false, $"Error: {ex.Message}");
                return false;
            }
        }

        public async Task<bool> ConfirmInventoryAdjustmentAsync(InventoryAdjustmentContext context)
        {
            var transactionId = _auditService.StartTransaction("InventoryAdjustment", 
                $"Adjusting inventory for {context.ProductName}", 
                new Dictionary<string, object>
                {
                    ["ProductId"] = context.ProductId,
                    ["ProductName"] = context.ProductName,
                    ["CurrentQuantity"] = context.CurrentQuantity,
                    ["AdjustmentQuantity"] = context.AdjustmentQuantity,
                    ["AdjustmentType"] = context.AdjustmentType,
                    ["Reason"] = context.Reason
                });

            try
            {
                var impact = BuildInventoryAdjustmentImpact(context);
                
                _logger.LogInformation("[INVENTORY_ADJUSTMENT_CONFIRM] [TX:{TransactionId}] [User:{User}] Requesting confirmation for inventory adjustment", 
                    transactionId, _currentUserService.Username);

                var confirmed = await _confirmationDialog.ConfirmStockAdjustmentAsync(
                    context.ProductName,
                    context.CurrentQuantity,
                    context.NewQuantity,
                    context.Reason);

                if (confirmed)
                {
                    var adjustmentAmount = context.AdjustmentType == "Add" ? context.AdjustmentQuantity : -context.AdjustmentQuantity;
                    _auditService.LogFinancialAction(transactionId, "InventoryAdjusted", adjustmentAmount, 
                        $"Product:{context.ProductId}", context.Reason);
                }

                _auditService.CompleteTransaction(transactionId, confirmed, 
                    confirmed ? "Inventory adjustment confirmed by user" : "Inventory adjustment cancelled by user");

                return confirmed;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[INVENTORY_ADJUSTMENT_CONFIRM_ERROR] [TX:{TransactionId}] Error during inventory adjustment confirmation", 
                    transactionId);
                _auditService.CompleteTransaction(transactionId, false, $"Error: {ex.Message}");
                return false;
            }
        }

        public async Task<bool> ConfirmBulkOperationAsync(BulkOperationContext context)
        {
            var transactionId = _auditService.StartTransaction("BulkOperation", 
                $"Bulk {context.OperationType} on {context.RecordCount} records", 
                new Dictionary<string, object>
                {
                    ["OperationType"] = context.OperationType,
                    ["RecordCount"] = context.RecordCount,
                    ["EntityType"] = context.EntityType
                });

            try
            {
                var impact = BuildBulkOperationImpact(context);
                
                _logger.LogInformation("[BULK_OPERATION_CONFIRM] [TX:{TransactionId}] [User:{User}] Requesting confirmation for bulk operation", 
                    transactionId, _currentUserService.Username);

                var confirmed = await _confirmationDialog.ConfirmBulkOperationAsync(
                    context.OperationType,
                    context.RecordCount,
                    context.AdditionalInfo);

                if (confirmed)
                {
                    _auditService.LogSecurityEvent(transactionId, "BulkOperation", 
                        $"{context.EntityType}:{context.RecordCount}", $"Bulk {context.OperationType} by {_currentUserService.Username}");
                }

                _auditService.CompleteTransaction(transactionId, confirmed, 
                    confirmed ? "Bulk operation confirmed by user" : "Bulk operation cancelled by user");

                return confirmed;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[BULK_OPERATION_CONFIRM_ERROR] [TX:{TransactionId}] Error during bulk operation confirmation", 
                    transactionId);
                _auditService.CompleteTransaction(transactionId, false, $"Error: {ex.Message}");
                return false;
            }
        }

        private string BuildInvoicePostingImpact(InvoicePostingContext context)
        {
            return $@"
INVOICE POSTING IMPACT SUMMARY
==============================

Invoice: {context.InvoiceNumber}
Customer: {context.CustomerName}
Total Amount: {context.TotalAmount:C}
Line Items: {context.LineCount}

THIS ACTION WILL:
• Update customer account balance by {context.TotalAmount:C}
• Create accounting entries for all line items
• Generate tax records
• Create audit trail
• Update inventory (if applicable)
• Mark invoice as posted (cannot be unposted)

IMPACT ON REPORTS:
• Sales reports will include this invoice
• Customer statements will show this transaction
• Tax reports will include this transaction
• Inventory reports will reflect any stock changes

WARNING: This action is IRREVERSIBLE.
Once posted, the invoice cannot be modified or deleted.
";
        }

        private string BuildPeriodClosingImpact(PeriodClosingContext context)
        {
            return $@"
PERIOD CLOSING IMPACT SUMMARY
===============================

Period: {context.PeriodName}
End Date: {context.EndDate:yyyy-MM-dd}
Transactions: {context.TransactionCount}

THIS ACTION WILL:
• Lock the accounting period permanently
• Prevent new transactions in this period
• Generate period-end reports
• Calculate period totals
• Create closing journal entries
• Archive period data

IMPACT ON OPERATIONS:
• No new invoices can be created for this period
• No adjustments can be made to this period
• All reports for this period become final
• Tax calculations for this period are locked

WARNING: This action is IRREVERSIBLE.
Once closed, the period cannot be reopened.
All data for this period becomes read-only.
";
        }

        private string BuildInventoryAdjustmentImpact(InventoryAdjustmentContext context)
        {
            var adjustmentType = context.AdjustmentType == "Add" ? "Increase" : "Decrease";
            var newQuantity = context.AdjustmentType == "Add" 
                ? context.CurrentQuantity + context.AdjustmentQuantity 
                : context.CurrentQuantity - context.AdjustmentQuantity;

            return $@"
INVENTORY ADJUSTMENT IMPACT SUMMARY
===================================

Product: {context.ProductName}
Current Quantity: {context.CurrentQuantity}
Adjustment: {adjustmentType} by {context.AdjustmentQuantity}
New Quantity: {newQuantity}
Reason: {context.Reason}

THIS ACTION WILL:
• Update inventory levels immediately
• Create inventory transaction record
• Generate audit trail
• Update inventory valuation
• Create journal entry for cost adjustment

IMPACT ON OPERATIONS:
• Stock availability will change
• Inventory reports will reflect new levels
• Cost of goods sold may be affected
• Reorder points may need adjustment

WARNING: This action is IRREVERSIBLE.
Once processed, the inventory adjustment cannot be undone.
This will affect all subsequent inventory transactions.
";
        }

        private string BuildBulkOperationImpact(BulkOperationContext context)
        {
            return $@"
BULK OPERATION IMPACT SUMMARY
=============================

Operation: {context.OperationType}
Entity Type: {context.EntityType}
Record Count: {context.RecordCount}
Additional Info: {context.AdditionalInfo}

THIS ACTION WILL:
• Process {context.RecordCount} records
• Create audit trail for each record
• Generate transaction logs
• Update database records
• May trigger related business rules

IMPACT ON SYSTEM:
• Database will be updated for all records
• Reports will reflect changes
• User permissions will be checked for each record
• System performance may be affected during processing

WARNING: This action is IRREVERSIBLE.
Once started, the bulk operation cannot be stopped.
All {context.RecordCount} records will be processed.
";
        }
    }

    public interface IIrreversibleActionProtectionService
    {
        Task<bool> ConfirmInvoicePostingAsync(InvoicePostingContext context);
        Task<bool> ConfirmPeriodClosingAsync(PeriodClosingContext context);
        Task<bool> ConfirmInventoryAdjustmentAsync(InventoryAdjustmentContext context);
        Task<bool> ConfirmBulkOperationAsync(BulkOperationContext context);
    }

    // Context classes for different irreversible actions
    public class InvoicePostingContext
    {
        public string InvoiceNumber { get; set; } = string.Empty;
        public Guid CustomerId { get; set; }
        public string CustomerName { get; set; } = string.Empty;
        public decimal TotalAmount { get; set; }
        public int LineCount { get; set; }
    }

    public class PeriodClosingContext
    {
        public Guid PeriodId { get; set; }
        public string PeriodName { get; set; } = string.Empty;
        public DateTime EndDate { get; set; }
        public int TransactionCount { get; set; }
    }

    public class InventoryAdjustmentContext
    {
        public Guid ProductId { get; set; }
        public string ProductName { get; set; } = string.Empty;
        public decimal CurrentQuantity { get; set; }
        public decimal AdjustmentQuantity { get; set; }
        public string AdjustmentType { get; set; } = string.Empty; // "Add" or "Remove"
        public decimal NewQuantity => AdjustmentType == "Add" 
            ? CurrentQuantity + AdjustmentQuantity 
            : CurrentQuantity - AdjustmentQuantity;
        public string Reason { get; set; } = string.Empty;
    }

    public class BulkOperationContext
    {
        public string OperationType { get; set; } = string.Empty;
        public string EntityType { get; set; } = string.Empty;
        public int RecordCount { get; set; }
        public string AdditionalInfo { get; set; } = string.Empty;
    }
}
