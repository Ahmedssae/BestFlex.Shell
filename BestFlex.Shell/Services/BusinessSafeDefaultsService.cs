using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Extensions.Logging;
using BestFlex.Application.Abstractions;

namespace BestFlex.Shell.Services
{
    /// <summary>
    /// Enforces business-safe defaults for critical screens to prevent data corruption
    /// </summary>
    public class BusinessSafeDefaultsService : IBusinessSafeDefaultsService
    {
        private readonly ILogger<BusinessSafeDefaultsService> _logger;
        private readonly ICurrentUserService _currentUserService;

        public BusinessSafeDefaultsService(
            ILogger<BusinessSafeDefaultsService> logger,
            ICurrentUserService currentUserService)
        {
            _logger = logger;
            _currentUserService = currentUserService;
        }

        public ValidationResult ValidateSalesOrder(SalesOrderValidationData data)
        {
            var errors = new List<string>();
            var warnings = new List<string>();

            // Customer must be explicitly selected
            if (data.CustomerId == null || data.CustomerId == Guid.Empty)
            {
                errors.Add("Customer must be selected before creating a sales order");
            }

            // No default quantities - user must explicitly enter
            if (data.Lines.Any(l => l.Quantity <= 0))
            {
                errors.Add("All line items must have a positive quantity");
            }

            // No auto-selected products
            if (data.Lines.Any(l => l.ProductId == null || l.ProductId == Guid.Empty))
            {
                errors.Add("All line items must have a product selected");
            }

            // Validate unit prices
            if (data.Lines.Any(l => l.UnitPrice <= 0))
            {
                warnings.Add("Some line items have zero or negative unit prices");
            }

            // Check for duplicate products
            var duplicateProducts = data.Lines
                .Where(l => l.ProductId != null)
                .GroupBy(l => l.ProductId)
                .Where(g => g.Count() > 1)
                .Select(g => g.Key);

            if (duplicateProducts.Any())
            {
                errors.Add("Duplicate products found in order lines");
            }

            // Validate totals
            var calculatedTotal = data.Lines.Sum(l => l.Quantity * l.UnitPrice);
            if (Math.Abs(calculatedTotal - data.Total) > 0.01m)
            {
                errors.Add($"Order total mismatch. Expected: {calculatedTotal:C}, Provided: {data.Total:C}");
            }

            var result = new ValidationResult
            {
                IsValid = !errors.Any(),
                Errors = errors,
                Warnings = warnings
            };

            if (!result.IsValid)
            {
                _logger.LogWarning("[SALES_ORDER_VALIDATION] [User:{User}] Sales order validation failed: {Errors}", 
                    _currentUserService.Username, string.Join("; ", errors));
            }

            return result;
        }

        public ValidationResult ValidateInvoicePosting(InvoiceValidationData data)
        {
            var errors = new List<string>();
            var warnings = new List<string>();

            // Customer must be explicitly selected
            if (data.CustomerId == null || data.CustomerId == Guid.Empty)
            {
                errors.Add("Customer must be selected before posting an invoice");
            }

            // Invoice must have lines
            if (!data.Lines.Any())
            {
                errors.Add("Invoice must have at least one line item");
            }

            // Validate line items
            foreach (var line in data.Lines)
            {
                if (line.ProductId == null || line.ProductId == Guid.Empty)
                {
                    errors.Add("All invoice lines must have a product selected");
                }

                if (line.Quantity <= 0)
                {
                    errors.Add("All invoice lines must have positive quantities");
                }

                if (line.UnitPrice < 0)
                {
                    errors.Add("Invoice line prices cannot be negative");
                }
            }

            // Validate totals
            var calculatedSubtotal = data.Lines.Sum(l => l.Quantity * l.UnitPrice);
            if (Math.Abs(calculatedSubtotal - data.Subtotal) > 0.01m)
            {
                errors.Add($"Invoice subtotal mismatch. Expected: {calculatedSubtotal:C}, Provided: {data.Subtotal:C}");
            }

            var calculatedTotal = calculatedSubtotal + data.Tax - data.Discount;
            if (Math.Abs(calculatedTotal - data.Total) > 0.01m)
            {
                errors.Add($"Invoice total mismatch. Expected: {calculatedTotal:C}, Provided: {data.Total:C}");
            }

            // Check for duplicate products
            var duplicateProducts = data.Lines
                .Where(l => l.ProductId != null)
                .GroupBy(l => l.ProductId)
                .Where(g => g.Count() > 1)
                .Select(g => g.Key);

            if (duplicateProducts.Any())
            {
                warnings.Add("Duplicate products found in invoice lines");
            }

            // Validate posting date
            if (data.PostingDate == default)
            {
                errors.Add("Posting date must be specified");
            }

            if (data.PostingDate > DateTime.Today)
            {
                warnings.Add("Posting date is in the future");
            }

            var result = new ValidationResult
            {
                IsValid = !errors.Any(),
                Errors = errors,
                Warnings = warnings
            };

            if (!result.IsValid)
            {
                _logger.LogWarning("[INVOICE_POSTING_VALIDATION] [User:{User}] Invoice posting validation failed: {Errors}", 
                    _currentUserService.Username, string.Join("; ", errors));
            }

            return result;
        }

        public ValidationResult ValidateInventoryAdjustment(InventoryAdjustmentValidationData data)
        {
            var errors = new List<string>();
            var warnings = new List<string>();

            // Product must be explicitly selected
            if (data.ProductId == null || data.ProductId == Guid.Empty)
            {
                errors.Add("Product must be selected for inventory adjustment");
            }

            // Reason must be provided
            if (string.IsNullOrWhiteSpace(data.Reason))
            {
                errors.Add("Reason for inventory adjustment must be specified");
            }

            // Quantity cannot be zero (no adjustment)
            if (data.AdjustmentQuantity == 0)
            {
                errors.Add("Adjustment quantity cannot be zero");
            }

            // Validate adjustment type
            if (data.AdjustmentType != InventoryAdjustmentType.Add && 
                data.AdjustmentType != InventoryAdjustmentType.Remove)
            {
                errors.Add("Adjustment type must be either Add or Remove");
            }

            // Check for negative inventory
            if (data.AdjustmentType == InventoryAdjustmentType.Remove && 
                data.CurrentQuantity - data.AdjustmentQuantity < 0)
            {
                errors.Add($"Adjustment would result in negative inventory. Current: {data.CurrentQuantity}, Adjustment: {data.AdjustmentQuantity}");
            }

            // Validate adjustment date
            if (data.AdjustmentDate == default)
            {
                errors.Add("Adjustment date must be specified");
            }

            if (data.AdjustmentDate > DateTime.Today)
            {
                warnings.Add("Adjustment date is in the future");
            }

            // Large quantity adjustments require additional confirmation
            if (Math.Abs(data.AdjustmentQuantity) > 1000)
            {
                warnings.Add($"Large quantity adjustment detected: {data.AdjustmentQuantity}");
            }

            var result = new ValidationResult
            {
                IsValid = !errors.Any(),
                Errors = errors,
                Warnings = warnings
            };

            if (!result.IsValid)
            {
                _logger.LogWarning("[INVENTORY_ADJUSTMENT_VALIDATION] [User:{User}] Inventory adjustment validation failed: {Errors}", 
                    _currentUserService.Username, string.Join("; ", errors));
            }

            return result;
        }

        public ValidationResult ValidatePeriodClosing(PeriodClosingValidationData data)
        {
            var errors = new List<string>();
            var warnings = new List<string>();

            // Period must be specified
            if (data.PeriodId == null || data.PeriodId == Guid.Empty)
            {
                errors.Add("Period must be selected for closing");
            }

            // Period must not already be closed
            if (data.IsAlreadyClosed)
            {
                errors.Add("Period is already closed");
            }

            // End date must be specified
            if (data.EndDate == default)
            {
                errors.Add("Period end date must be specified");
            }

            // Cannot close future periods
            if (data.EndDate > DateTime.Today)
            {
                errors.Add("Cannot close future periods");
            }

            // Check for unposted transactions
            if (data.HasUnpostedTransactions)
            {
                errors.Add("Period has unposted transactions that must be posted before closing");
            }

            // Check for unbalanced journal entries
            if (data.HasUnbalancedEntries)
            {
                errors.Add("Period has unbalanced journal entries that must be corrected before closing");
            }

            // Warn about recent transactions
            if (data.HasRecentTransactions)
            {
                warnings.Add("Period has recent transactions. Please verify all transactions are correct before closing.");
            }

            var result = new ValidationResult
            {
                IsValid = !errors.Any(),
                Errors = errors,
                Warnings = warnings
            };

            if (!result.IsValid)
            {
                _logger.LogWarning("[PERIOD_CLOSING_VALIDATION] [User:{User}] Period closing validation failed: {Errors}", 
                    _currentUserService.Username, string.Join("; ", errors));
            }

            return result;
        }

        public string GetSafeDefaultMessage(string operation, string field)
        {
            return operation switch
            {
                "SalesOrder" when field == "Customer" => "Please select a customer from the dropdown list",
                "SalesOrder" when field == "Product" => "Please select a product for each line item",
                "SalesOrder" when field == "Quantity" => "Please enter a positive quantity for each line item",
                "Invoice" when field == "Customer" => "Please select a customer before posting the invoice",
                "Invoice" when field == "Lines" => "Please add at least one line item to the invoice",
                "Inventory" when field == "Product" => "Please select a product for the inventory adjustment",
                "Inventory" when field == "Reason" => "Please specify a reason for the inventory adjustment",
                "Period" when field == "Period" => "Please select a valid accounting period to close",
                _ => "Please provide a valid value for this field"
            };
        }
    }

    public interface IBusinessSafeDefaultsService
    {
        ValidationResult ValidateSalesOrder(SalesOrderValidationData data);
        ValidationResult ValidateInvoicePosting(InvoiceValidationData data);
        ValidationResult ValidateInventoryAdjustment(InventoryAdjustmentValidationData data);
        ValidationResult ValidatePeriodClosing(PeriodClosingValidationData data);
        string GetSafeDefaultMessage(string operation, string field);
    }

    public class ValidationResult
    {
        public bool IsValid { get; set; }
        public List<string> Errors { get; set; } = new();
        public List<string> Warnings { get; set; } = new();
        public string GetDisplayMessage()
        {
            var messages = new List<string>();
            
            if (Errors.Any())
            {
                messages.Add("Please correct the following errors:");
                messages.AddRange(Errors.Select(e => $"• {e}"));
            }
            
            if (Warnings.Any())
            {
                if (messages.Any()) messages.Add("");
                messages.Add("Warnings:");
                messages.AddRange(Warnings.Select(w => $"• {w}"));
            }
            
            return string.Join(Environment.NewLine, messages);
        }
    }

    // Validation data classes
    public class SalesOrderValidationData
    {
        public Guid? CustomerId { get; set; }
        public List<SalesOrderLineValidationData> Lines { get; set; } = new();
        public decimal Total { get; set; }
    }

    public class SalesOrderLineValidationData
    {
        public Guid? ProductId { get; set; }
        public decimal Quantity { get; set; }
        public decimal UnitPrice { get; set; }
    }

    public class InvoiceValidationData
    {
        public Guid? CustomerId { get; set; }
        public List<InvoiceLineValidationData> Lines { get; set; } = new();
        public decimal Subtotal { get; set; }
        public decimal Tax { get; set; }
        public decimal Discount { get; set; }
        public decimal Total { get; set; }
        public DateTime PostingDate { get; set; }
    }

    public class InvoiceLineValidationData
    {
        public Guid? ProductId { get; set; }
        public decimal Quantity { get; set; }
        public decimal UnitPrice { get; set; }
    }

    public class InventoryAdjustmentValidationData
    {
        public Guid? ProductId { get; set; }
        public decimal AdjustmentQuantity { get; set; }
        public InventoryAdjustmentType AdjustmentType { get; set; }
        public decimal CurrentQuantity { get; set; }
        public string Reason { get; set; } = string.Empty;
        public DateTime AdjustmentDate { get; set; }
    }

    public enum InventoryAdjustmentType
    {
        Add,
        Remove
    }

    public class PeriodClosingValidationData
    {
        public Guid? PeriodId { get; set; }
        public DateTime EndDate { get; set; }
        public bool IsAlreadyClosed { get; set; }
        public bool HasUnpostedTransactions { get; set; }
        public bool HasUnbalancedEntries { get; set; }
        public bool HasRecentTransactions { get; set; }
    }
}
