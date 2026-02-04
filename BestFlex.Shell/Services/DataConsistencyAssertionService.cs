using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Extensions.Logging;
using BestFlex.Application.Abstractions;

namespace BestFlex.Shell.Services
{
    /// <summary>
    /// Provides data consistency assertions to prevent data corruption
    /// </summary>
    public class DataConsistencyAssertionService : IDataConsistencyAssertionService
    {
        private readonly ILogger<DataConsistencyAssertionService> _logger;
        private readonly ICurrentUserService _currentUserService;
        private readonly IAuditConfidenceService _auditService;

        public DataConsistencyAssertionService(
            ILogger<DataConsistencyAssertionService> logger,
            ICurrentUserService currentUserService,
            IAuditConfidenceService auditService)
        {
            _logger = logger;
            _currentUserService = currentUserService;
            _auditService = auditService;
        }

        public ConsistencyResult ValidateInvoiceConsistency(InvoiceConsistencyData data)
        {
            var transactionId = _auditService.StartTransaction("InvoiceConsistencyCheck", 
                $"Validating invoice {data.InvoiceNumber}", 
                new Dictionary<string, object>
                {
                    ["InvoiceNumber"] = data.InvoiceNumber,
                    ["CustomerId"] = data.CustomerId,
                    ["LineCount"] = data.Lines.Count
                });

            try
            {
                var errors = new List<string>();
                var warnings = new List<string>();

                // Calculate expected totals
                var calculatedSubtotal = data.Lines.Sum(l => l.Quantity * l.UnitPrice);
                var calculatedTax = CalculateTax(calculatedSubtotal, data.TaxRate);
                var calculatedTotal = calculatedSubtotal + calculatedTax - data.Discount;

                // Validate subtotal
                if (Math.Abs(calculatedSubtotal - data.Subtotal) > 0.01m)
                {
                    errors.Add($"Invoice subtotal mismatch. Expected: {calculatedSubtotal:C}, Provided: {data.Subtotal:C}");
                }

                // Validate tax
                if (Math.Abs(calculatedTax - data.Tax) > 0.01m)
                {
                    errors.Add($"Invoice tax mismatch. Expected: {calculatedTax:C}, Provided: {data.Tax:C}");
                }

                // Validate total
                if (Math.Abs(calculatedTotal - data.Total) > 0.01m)
                {
                    errors.Add($"Invoice total mismatch. Expected: {calculatedTotal:C}, Provided: {data.Total:C}");
                }

                // Validate line items
                foreach (var line in data.Lines)
                {
                    if (line.Quantity <= 0)
                    {
                        errors.Add($"Line item for product {line.ProductName} has invalid quantity: {line.Quantity}");
                    }

                    if (line.UnitPrice < 0)
                    {
                        errors.Add($"Line item for product {line.ProductName} has negative price: {line.UnitPrice:C}");
                    }

                    if (line.Total != line.Quantity * line.UnitPrice)
                    {
                        errors.Add($"Line item total mismatch for {line.ProductName}. Expected: {line.Quantity * line.UnitPrice:C}, Provided: {line.Total:C}");
                    }
                }

                // Check for duplicate products
                var duplicateProducts = data.Lines
                    .Where(l => l.ProductId != Guid.Empty)
                    .GroupBy(l => l.ProductId)
                    .Where(g => g.Count() > 1)
                    .Select(g => g.Key);

                if (duplicateProducts.Any())
                {
                    warnings.Add("Duplicate products found in invoice lines");
                }

                // Validate customer
                if (data.CustomerId == Guid.Empty)
                {
                    errors.Add("Invoice must have a valid customer");
                }

                var result = new ConsistencyResult
                {
                    IsValid = !errors.Any(),
                    Errors = errors,
                    Warnings = warnings,
                    TransactionId = transactionId
                };

                if (!result.IsValid)
                {
                    _logger.LogError("[INVOICE_CONSISTENCY] [TX:{TransactionId}] [User:{User}] Invoice consistency check failed: {Errors}", 
                        transactionId, _currentUserService.Username, string.Join("; ", errors));

                    _auditService.LogSecurityEvent(transactionId, "InvoiceConsistencyFailure", 
                        $"Invoice:{data.InvoiceNumber}", string.Join("; ", errors));
                }

                _auditService.CompleteTransaction(transactionId, result.IsValid, 
                    result.IsValid ? "Invoice consistency check passed" : "Invoice consistency check failed");

                return result;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[INVOICE_CONSISTENCY_ERROR] [TX:{TransactionId}] Error during invoice consistency check", 
                    transactionId);
                _auditService.CompleteTransaction(transactionId, false, $"Error: {ex.Message}");
                
                return new ConsistencyResult
                {
                    IsValid = false,
                    Errors = new List<string> { $"Consistency check error: {ex.Message}" },
                    TransactionId = transactionId
                };
            }
        }

        public ConsistencyResult ValidateJournalEntryConsistency(JournalEntryConsistencyData data)
        {
            var transactionId = _auditService.StartTransaction("JournalEntryConsistencyCheck", 
                $"Validating journal entry {data.EntryNumber}", 
                new Dictionary<string, object>
                {
                    ["EntryNumber"] = data.EntryNumber,
                    ["LineCount"] = data.Lines.Count,
                    ["EntryDate"] = data.EntryDate
                });

            try
            {
                var errors = new List<string>();
                var warnings = new List<string>();

                // Calculate totals for debit and credit
                var totalDebit = data.Lines.Where(l => l.Debit > 0).Sum(l => l.Debit);
                var totalCredit = data.Lines.Where(l => l.Credit > 0).Sum(l => l.Credit);

                // Validate balance
                if (Math.Abs(totalDebit - totalCredit) > 0.01m)
                {
                    errors.Add($"Journal entry is not balanced. Debit: {totalDebit:C}, Credit: {totalCredit:C}, Difference: {Math.Abs(totalDebit - totalCredit):C}");
                }

                // Validate line items
                foreach (var line in data.Lines)
                {
                    if (line.Debit > 0 && line.Credit > 0)
                    {
                        errors.Add($"Journal entry line cannot have both debit and credit amounts. Account: {line.AccountNumber}");
                    }

                    if (line.Debit <= 0 && line.Credit <= 0)
                    {
                        errors.Add($"Journal entry line must have either debit or credit amount. Account: {line.AccountNumber}");
                    }

                    if (string.IsNullOrWhiteSpace(line.AccountNumber))
                    {
                        errors.Add("Journal entry line must have a valid account number");
                    }

                    if (line.Debit < 0 || line.Credit < 0)
                    {
                        errors.Add($"Journal entry amounts cannot be negative. Account: {line.AccountNumber}");
                    }
                }

                // Validate entry date
                if (data.EntryDate == default)
                {
                    errors.Add("Journal entry must have a valid date");
                }

                // Validate entry number
                if (string.IsNullOrWhiteSpace(data.EntryNumber))
                {
                    errors.Add("Journal entry must have a valid entry number");
                }

                // Check for duplicate accounts
                var duplicateAccounts = data.Lines
                    .GroupBy(l => l.AccountNumber)
                    .Where(g => g.Count() > 1)
                    .Select(g => g.Key);

                if (duplicateAccounts.Any())
                {
                    warnings.Add($"Duplicate accounts found in journal entry: {string.Join(", ", duplicateAccounts)}");
                }

                var result = new ConsistencyResult
                {
                    IsValid = !errors.Any(),
                    Errors = errors,
                    Warnings = warnings,
                    TransactionId = transactionId
                };

                if (!result.IsValid)
                {
                    _logger.LogError("[JOURNAL_CONSISTENCY] [TX:{TransactionId}] [User:{User}] Journal entry consistency check failed: {Errors}", 
                        transactionId, _currentUserService.Username, string.Join("; ", errors));

                    _auditService.LogSecurityEvent(transactionId, "JournalConsistencyFailure", 
                        $"JournalEntry:{data.EntryNumber}", string.Join("; ", errors));
                }

                _auditService.CompleteTransaction(transactionId, result.IsValid, 
                    result.IsValid ? "Journal entry consistency check passed" : "Journal entry consistency check failed");

                return result;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[JOURNAL_CONSISTENCY_ERROR] [TX:{TransactionId}] Error during journal entry consistency check", 
                    transactionId);
                _auditService.CompleteTransaction(transactionId, false, $"Error: {ex.Message}");
                
                return new ConsistencyResult
                {
                    IsValid = false,
                    Errors = new List<string> { $"Consistency check error: {ex.Message}" },
                    TransactionId = transactionId
                };
            }
        }

        public ConsistencyResult ValidateInventoryConsistency(InventoryConsistencyData data)
        {
            var transactionId = _auditService.StartTransaction("InventoryConsistencyCheck", 
                $"Validating inventory for {data.ProductName}", 
                new Dictionary<string, object>
                {
                    ["ProductId"] = data.ProductId,
                    ["ProductName"] = data.ProductName,
                    ["CurrentQuantity"] = data.CurrentQuantity,
                    ["ReservedQuantity"] = data.ReservedQuantity
                });

            try
            {
                var errors = new List<string>();
                var warnings = new List<string>();

                // Validate available quantity
                var availableQuantity = data.CurrentQuantity - data.ReservedQuantity;
                if (availableQuantity < 0)
                {
                    errors.Add($"Available quantity cannot be negative. Current: {data.CurrentQuantity}, Reserved: {data.ReservedQuantity}, Available: {availableQuantity}");
                }

                // Validate current quantity
                if (data.CurrentQuantity < 0)
                {
                    errors.Add($"Current quantity cannot be negative: {data.CurrentQuantity}");
                }

                // Validate reserved quantity
                if (data.ReservedQuantity < 0)
                {
                    errors.Add($"Reserved quantity cannot be negative: {data.ReservedQuantity}");
                }

                // Check for negative stock after pending transactions
                if (data.PendingOutbound > availableQuantity)
                {
                    errors.Add($"Pending outbound transactions ({data.PendingOutbound}) exceed available quantity ({availableQuantity})");
                }

                // Validate reorder point
                if (data.ReorderPoint < 0)
                {
                    errors.Add($"Reorder point cannot be negative: {data.ReorderPoint}");
                }

                // Check for low stock warning
                if (availableQuantity <= data.ReorderPoint && availableQuantity > 0)
                {
                    warnings.Add($"Stock is at or below reorder point. Available: {availableQuantity}, Reorder point: {data.ReorderPoint}");
                }

                // Check for out of stock
                if (availableQuantity == 0)
                {
                    warnings.Add("Product is out of stock");
                }

                var result = new ConsistencyResult
                {
                    IsValid = !errors.Any(),
                    Errors = errors,
                    Warnings = warnings,
                    TransactionId = transactionId
                };

                if (!result.IsValid)
                {
                    _logger.LogError("[INVENTORY_CONSISTENCY] [TX:{TransactionId}] [User:{User}] Inventory consistency check failed: {Errors}", 
                        transactionId, _currentUserService.Username, string.Join("; ", errors));

                    _auditService.LogSecurityEvent(transactionId, "InventoryConsistencyFailure", 
                        $"Product:{data.ProductName}", string.Join("; ", errors));
                }

                _auditService.CompleteTransaction(transactionId, result.IsValid, 
                    result.IsValid ? "Inventory consistency check passed" : "Inventory consistency check failed");

                return result;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[INVENTORY_CONSISTENCY_ERROR] [TX:{TransactionId}] Error during inventory consistency check", 
                    transactionId);
                _auditService.CompleteTransaction(transactionId, false, $"Error: {ex.Message}");
                
                return new ConsistencyResult
                {
                    IsValid = false,
                    Errors = new List<string> { $"Consistency check error: {ex.Message}" },
                    TransactionId = transactionId
                };
            }
        }

        public ConsistencyResult ValidateSalesOrderConsistency(SalesOrderConsistencyData data)
        {
            var transactionId = _auditService.StartTransaction("SalesOrderConsistencyCheck", 
                $"Validating sales order {data.OrderNumber}", 
                new Dictionary<string, object>
                {
                    ["OrderNumber"] = data.OrderNumber,
                    ["CustomerId"] = data.CustomerId,
                    ["LineCount"] = data.Lines.Count
                });

            try
            {
                var errors = new List<string>();
                var warnings = new List<string>();

                // Calculate expected total
                var calculatedTotal = data.Lines.Sum(l => l.Quantity * l.UnitPrice);

                // Validate total
                if (Math.Abs(calculatedTotal - data.Total) > 0.01m)
                {
                    errors.Add($"Sales order total mismatch. Expected: {calculatedTotal:C}, Provided: {data.Total:C}");
                }

                // Validate line items
                foreach (var line in data.Lines)
                {
                    if (line.Quantity <= 0)
                    {
                        errors.Add($"Line item for product {line.ProductName} has invalid quantity: {line.Quantity}");
                    }

                    if (line.UnitPrice < 0)
                    {
                        errors.Add($"Line item for product {line.ProductName} has negative price: {line.UnitPrice:C}");
                    }

                    // Check inventory availability
                    if (line.AvailableQuantity < line.Quantity)
                    {
                        errors.Add($"Insufficient stock for {line.ProductName}. Available: {line.AvailableQuantity}, Required: {line.Quantity}");
                    }
                }

                // Validate customer
                if (data.CustomerId == Guid.Empty)
                {
                    errors.Add("Sales order must have a valid customer");
                }

                // Check for duplicate products
                var duplicateProducts = data.Lines
                    .Where(l => l.ProductId != Guid.Empty)
                    .GroupBy(l => l.ProductId)
                    .Where(g => g.Count() > 1)
                    .Select(g => g.Key);

                if (duplicateProducts.Any())
                {
                    errors.Add("Duplicate products found in sales order lines");
                }

                var result = new ConsistencyResult
                {
                    IsValid = !errors.Any(),
                    Errors = errors,
                    Warnings = warnings,
                    TransactionId = transactionId
                };

                if (!result.IsValid)
                {
                    _logger.LogError("[SALES_ORDER_CONSISTENCY] [TX:{TransactionId}] [User:{User}] Sales order consistency check failed: {Errors}", 
                        transactionId, _currentUserService.Username, string.Join("; ", errors));

                    _auditService.LogSecurityEvent(transactionId, "SalesOrderConsistencyFailure", 
                        $"Order:{data.OrderNumber}", string.Join("; ", errors));
                }

                _auditService.CompleteTransaction(transactionId, result.IsValid, 
                    result.IsValid ? "Sales order consistency check passed" : "Sales order consistency check failed");

                return result;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[SALES_ORDER_CONSISTENCY_ERROR] [TX:{TransactionId}] Error during sales order consistency check", 
                    transactionId);
                _auditService.CompleteTransaction(transactionId, false, $"Error: {ex.Message}");
                
                return new ConsistencyResult
                {
                    IsValid = false,
                    Errors = new List<string> { $"Consistency check error: {ex.Message}" },
                    TransactionId = transactionId
                };
            }
        }

        private decimal CalculateTax(decimal subtotal, decimal taxRate)
        {
            return subtotal * (taxRate / 100m);
        }

        public string GetConsistencyErrorMessage(ConsistencyResult result)
        {
            if (result.IsValid)
                return string.Empty;

            var messages = new List<string>();
            
            if (result.Errors.Any())
            {
                messages.Add("CRITICAL ERRORS - Action Blocked:");
                messages.AddRange(result.Errors.Select(e => $"• {e}"));
            }
            
            if (result.Warnings.Any())
            {
                if (messages.Any()) messages.Add("");
                messages.Add("Warnings:");
                messages.AddRange(result.Warnings.Select(w => $"• {w}"));
            }
            
            messages.Add("");
            messages.Add($"Transaction ID: {result.TransactionId}");
            messages.Add("Please correct these issues before continuing.");

            return string.Join(Environment.NewLine, messages);
        }
    }

    public interface IDataConsistencyAssertionService
    {
        ConsistencyResult ValidateInvoiceConsistency(InvoiceConsistencyData data);
        ConsistencyResult ValidateJournalEntryConsistency(JournalEntryConsistencyData data);
        ConsistencyResult ValidateInventoryConsistency(InventoryConsistencyData data);
        ConsistencyResult ValidateSalesOrderConsistency(SalesOrderConsistencyData data);
        string GetConsistencyErrorMessage(ConsistencyResult result);
    }

    public class ConsistencyResult
    {
        public bool IsValid { get; set; }
        public List<string> Errors { get; set; } = new();
        public List<string> Warnings { get; set; } = new();
        public string TransactionId { get; set; } = string.Empty;
    }

    // Data classes for consistency validation
    public class InvoiceConsistencyData
    {
        public string InvoiceNumber { get; set; } = string.Empty;
        public Guid CustomerId { get; set; }
        public List<InvoiceLineConsistencyData> Lines { get; set; } = new();
        public decimal Subtotal { get; set; }
        public decimal Tax { get; set; }
        public decimal TaxRate { get; set; }
        public decimal Discount { get; set; }
        public decimal Total { get; set; }
    }

    public class InvoiceLineConsistencyData
    {
        public Guid ProductId { get; set; }
        public string ProductName { get; set; } = string.Empty;
        public decimal Quantity { get; set; }
        public decimal UnitPrice { get; set; }
        public decimal Total { get; set; }
    }

    public class JournalEntryConsistencyData
    {
        public string EntryNumber { get; set; } = string.Empty;
        public DateTime EntryDate { get; set; }
        public List<JournalLineConsistencyData> Lines { get; set; } = new();
    }

    public class JournalLineConsistencyData
    {
        public string AccountNumber { get; set; } = string.Empty;
        public decimal Debit { get; set; }
        public decimal Credit { get; set; }
    }

    public class InventoryConsistencyData
    {
        public Guid ProductId { get; set; }
        public string ProductName { get; set; } = string.Empty;
        public decimal CurrentQuantity { get; set; }
        public decimal ReservedQuantity { get; set; }
        public decimal PendingOutbound { get; set; }
        public decimal ReorderPoint { get; set; }
    }

    public class SalesOrderConsistencyData
    {
        public string OrderNumber { get; set; } = string.Empty;
        public Guid CustomerId { get; set; }
        public List<SalesOrderLineConsistencyData> Lines { get; set; } = new();
        public decimal Total { get; set; }
    }

    public class SalesOrderLineConsistencyData
    {
        public Guid ProductId { get; set; }
        public string ProductName { get; set; } = string.Empty;
        public decimal Quantity { get; set; }
        public decimal UnitPrice { get; set; }
        public decimal AvailableQuantity { get; set; }
    }
}
