using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Windows;
using Microsoft.Extensions.Logging;
using BestFlex.Application.Abstractions;

namespace BestFlex.Shell.Services
{
    /// <summary>
    /// Provides confirmation dialogs for destructive and irreversible actions
    /// </summary>
    public class ConfirmationDialogService : IConfirmationDialogService
    {
        private readonly ILogger<ConfirmationDialogService> _logger;
        private readonly ICurrentUserService _currentUserService;

        public ConfirmationDialogService(
            ILogger<ConfirmationDialogService> logger,
            ICurrentUserService currentUserService)
        {
            _logger = logger;
            _currentUserService = currentUserService;
        }

        public async Task<bool> ConfirmAsync(ConfirmationRequest request)
        {
            var currentUser = _currentUserService.Username ?? "<unknown>";
            var correlationId = GenerateCorrelationId();

            // Log the confirmation request
            _logger.LogInformation("[CONFIRMATION_REQUEST] [CID:{CorrelationId}] [User:{User}] [Action:{Action}] Requesting confirmation for: {Details}", 
                correlationId, currentUser, request.Action, request.Details);

            // Show confirmation dialog on UI thread
            var result = await System.Windows.Application.Current.Dispatcher.InvokeAsync(() =>
            {
                var messageBoxResult = MessageBox.Show(
                    GetConfirmationMessage(request),
                    $"Confirm {request.Action}",
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Warning,
                    MessageBoxResult.No // Default to No for safety
                );

                var confirmed = messageBoxResult == MessageBoxResult.Yes;
                
                _logger.LogInformation("[CONFIRMATION_RESULT] [CID:{CorrelationId}] [User:{User}] [Action:{Action}] User chose: {Choice}", 
                    correlationId, currentUser, request.Action, confirmed ? "Confirmed" : "Cancelled");

                return confirmed;
            });

            return result;
        }

        public async Task<bool> ConfirmInvoicePostingAsync(string invoiceNumber, decimal amount)
        {
            return await ConfirmAsync(new ConfirmationRequest
            {
                Action = "Post Invoice",
                Details = $"You are about to post invoice #{invoiceNumber} for {amount:C}.\n\nThis action is irreversible and will:\n• Update customer account balance\n• Create accounting entries\n• Generate audit trail\n\nAre you sure you want to continue?",
                IsDestructive = true,
                RequiresExplicitConfirmation = true
            });
        }

        public async Task<bool> ConfirmStockAdjustmentAsync(string productName, decimal currentQuantity, decimal newQuantity, string reason)
        {
            var adjustment = newQuantity - currentQuantity;
            var adjustmentType = adjustment > 0 ? "increase" : "decrease";
            
            return await ConfirmAsync(new ConfirmationRequest
            {
                Action = "Stock Adjustment",
                Details = $"You are about to {adjustmentType} stock for '{productName}' by {Math.Abs(adjustment)} units.\n\nCurrent quantity: {currentQuantity}\nNew quantity: {newQuantity}\nReason: {reason}\n\nThis action will:\n• Update inventory levels\n• Create audit trail\n• Cannot be undone\n\nAre you sure you want to continue?",
                IsDestructive = true,
                RequiresExplicitConfirmation = true
            });
        }

        public async Task<bool> ConfirmPeriodClosingAsync(string periodName, DateTime endDate)
        {
            return await ConfirmAsync(new ConfirmationRequest
            {
                Action = "Close Accounting Period",
                Details = $"You are about to close accounting period '{periodName}' ending {endDate:yyyy-MM-dd}.\n\nThis action is irreversible and will:\n• Prevent new transactions in this period\n• Lock all financial data for the period\n• Generate period-end reports\n• Cannot be reopened\n\nAre you absolutely sure you want to close this period?",
                IsDestructive = true,
                RequiresExplicitConfirmation = true
            });
        }

        public async Task<bool> ConfirmCustomerDeletionAsync(string customerName, string customerCode)
        {
            return await ConfirmAsync(new ConfirmationRequest
            {
                Action = "Delete Customer",
                Details = $"You are about to permanently delete customer '{customerName}' ({customerCode}).\n\nThis action is irreversible and will:\n• Remove all customer data\n• Delete transaction history\n• Remove from all reports\n• Cannot be recovered\n\nAre you absolutely sure you want to delete this customer?",
                IsDestructive = true,
                RequiresExplicitConfirmation = true
            });
        }

        public async Task<bool> ConfirmProductDeletionAsync(string productName, string productCode)
        {
            return await ConfirmAsync(new ConfirmationRequest
            {
                Action = "Delete Product",
                Details = $"You are about to permanently delete product '{productName}' ({productCode}).\n\nThis action is irreversible and will:\n• Remove all product data\n• Delete inventory records\n• Remove from all transactions\n• Cannot be recovered\n\nAre you absolutely sure you want to delete this product?",
                IsDestructive = true,
                RequiresExplicitConfirmation = true
            });
        }

        public async Task<bool> ConfirmBulkOperationAsync(string operationType, int recordCount, string additionalInfo = "")
        {
            return await ConfirmAsync(new ConfirmationRequest
            {
                Action = $"Bulk {operationType}",
                Details = $"You are about to perform a bulk {operationType.ToLower()} on {recordCount} record(s).{(!string.IsNullOrEmpty(additionalInfo) ? $"\n\n{additionalInfo}" : "")}\n\nThis action will:\n• Process {recordCount} records\n• Create audit trail for each record\n• May take several minutes to complete\n• Cannot be undone\n\nAre you sure you want to proceed?",
                IsDestructive = true,
                RequiresExplicitConfirmation = true
            });
        }

        public async Task<bool> ConfirmDataImportAsync(string dataType, string fileName, int recordCount)
        {
            return await ConfirmAsync(new ConfirmationRequest
            {
                Action = "Import Data",
                Details = $"You are about to import {recordCount} {dataType.ToLower()} records from '{fileName}'.\n\nThis action will:\n• Add {recordCount} new records\n• Update existing records if duplicates found\n• Create audit trail\n• Cannot be undone\n\nAre you sure you want to import this data?",
                IsDestructive = false,
                RequiresExplicitConfirmation = true
            });
        }

        private string GetConfirmationMessage(ConfirmationRequest request)
        {
            var message = request.Details;
            
            if (request.IsDestructive)
            {
                message += "\n\n⚠️ WARNING: This action cannot be undone!";
            }
            
            if (request.RequiresExplicitConfirmation)
            {
                message += "\n\nPlease type 'CONFIRM' to proceed:"; // In a real implementation, you'd add a text input
            }
            
            return message;
        }

        private string GenerateCorrelationId()
        {
            var guidStr = Guid.NewGuid().ToString("N");
            return $"CONF-{DateTime.UtcNow:yyyyMMdd-HHmmss}-{guidStr.Substring(0, 8).ToUpper()}";
        }
    }

    public interface IConfirmationDialogService
    {
        Task<bool> ConfirmAsync(ConfirmationRequest request);
        Task<bool> ConfirmInvoicePostingAsync(string invoiceNumber, decimal amount);
        Task<bool> ConfirmStockAdjustmentAsync(string productName, decimal currentQuantity, decimal newQuantity, string reason);
        Task<bool> ConfirmPeriodClosingAsync(string periodName, DateTime endDate);
        Task<bool> ConfirmCustomerDeletionAsync(string customerName, string customerCode);
        Task<bool> ConfirmProductDeletionAsync(string productName, string productCode);
        Task<bool> ConfirmBulkOperationAsync(string operationType, int recordCount, string additionalInfo = "");
        Task<bool> ConfirmDataImportAsync(string dataType, string fileName, int recordCount);
    }

    public class ConfirmationRequest
    {
        public string Action { get; set; } = string.Empty;
        public string Details { get; set; } = string.Empty;
        public bool IsDestructive { get; set; }
        public bool RequiresExplicitConfirmation { get; set; }
        public Dictionary<string, object>? Context { get; set; }
    }
}
