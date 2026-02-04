using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using BestFlex.Domain.Entities;
using BestFlex.Shell.Services;

namespace BestFlex.Shell.Services
{
    public class PostingService : IPostingService
    {
        private readonly ISalesOrderRepository _salesOrderRepository;
        private readonly IAuditTrailService _auditTrailService;
        private readonly ILogger<PostingService> _logger;

        public PostingService(
            ISalesOrderRepository salesOrderRepository,
            IAuditTrailService auditTrailService,
            ILogger<PostingService> logger)
        {
            _salesOrderRepository = salesOrderRepository;
            _auditTrailService = auditTrailService;
            _logger = logger;
        }

        public async Task<PostingResult> PostOrderAsync(int orderId)
        {
            var transactionId = Guid.NewGuid().ToString("N")[..8].ToUpper();
            
            try
            {
                _logger.LogInformation("[TX:{TransactionId}] Starting posting process for Sales Order {OrderId}", 
                    transactionId, orderId);

                // Load the draft
                var order = await _salesOrderRepository.LoadDraftAsync(orderId);
                if (order == null)
                {
                    await _auditTrailService.LogValidationFailureAsync(orderId, "Order not found", "System");
                    return new PostingResult 
                    { 
                        Success = false, 
                        ErrorMessage = "Order not found",
                        AuditTrailEntry = $"TX:{transactionId} - Order not found"
                    };
                }

                // Check if posting is allowed
                var canPost = await CanPostOrderAsync(order);
                if (!canPost)
                {
                    var reason = "Order does not meet posting requirements";
                    await _auditTrailService.LogValidationFailureAsync(orderId, reason, "System");
                    return new PostingResult 
                    { 
                        Success = false, 
                        ErrorMessage = reason,
                        AuditTrailEntry = $"TX:{transactionId} - {reason}"
                    };
                }

                // Get user confirmation (simplified - in real app would show dialog)
                var confirmed = await ConfirmPostActionAsync(order);
                if (!confirmed)
                {
                    var reason = "Posting cancelled by user";
                    await _auditTrailService.LogValidationFailureAsync(orderId, reason, "System");
                    return new PostingResult 
                    { 
                        Success = false, 
                        ErrorMessage = reason,
                        AuditTrailEntry = $"TX:{transactionId} - {reason}"
                    };
                }

                // Create invoice
                var invoice = await CreateInvoiceAsync(order);
                if (invoice == null)
                {
                    var reason = "Failed to create invoice";
                    await _auditTrailService.LogValidationFailureAsync(orderId, reason, "System");
                    return new PostingResult 
                    { 
                        Success = false, 
                        ErrorMessage = reason,
                        AuditTrailEntry = $"TX:{transactionId} - {reason}"
                    };
                }

                // Link invoice to order and change status
                order.LinkToInvoice(invoice.Id);
                
                // Update the order
                var updated = await _salesOrderRepository.UpdateDraftAsync(order);
                if (updated == null)
                {
                    var reason = "Failed to update order status";
                    await _auditTrailService.LogValidationFailureAsync(orderId, reason, "System");
                    return new PostingResult 
                    { 
                        Success = false, 
                        ErrorMessage = reason,
                        AuditTrailEntry = $"TX:{transactionId} - {reason}"
                    };
                }

                // Log successful posting to audit trail
                var auditEntry = await _auditTrailService.LogPostingAsync(
                    order.Id, 
                    invoice.Id, 
                    invoice.InvoiceNumber, 
                    "CurrentUser");

                _logger.LogInformation("[TX:{TransactionId}] Successfully posted order {OrderId} and created invoice {InvoiceId}", 
                    transactionId, order.Id, invoice.Id);

                return new PostingResult 
                { 
                    Success = true, 
                    InvoiceId = invoice.Id,
                    InvoiceNumber = invoice.InvoiceNumber,
                    PostedAt = DateTime.UtcNow,
                    AuditTrailEntry = auditEntry
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[TX:{TransactionId}] Failed to post order {OrderId}", transactionId, orderId);
                
                await _auditTrailService.LogValidationFailureAsync(orderId, ex.Message, "System");
                
                return new PostingResult 
                { 
                    Success = false, 
                    ErrorMessage = "An error occurred while posting the order",
                    AuditTrailEntry = $"TX:{transactionId} - Exception: {ex.Message}"
                };
            }
        }

        private async Task<bool> CanPostOrderAsync(SalesOrder order)
        {
            // Business rules for posting
            if (order.Status != SalesOrderStatus.Draft)
            {
                _logger.LogWarning("Order {OrderId} is not in Draft status, cannot post", order.Id);
                return false;
            }

            if (!order.Lines.Any())
            {
                _logger.LogWarning("Order {OrderId} has no lines, cannot post", order.Id);
                return false;
            }

            // Check for negative quantities or prices
            if (order.Lines.Any(l => l.Quantity <= 0 || l.UnitPrice < 0))
            {
                _logger.LogWarning("Order {OrderId} has invalid line data, cannot post", order.Id);
                return false;
            }

            return true;
        }

        private async Task<bool> ConfirmPostActionAsync(SalesOrder order)
        {
            // In a real implementation, this would show a confirmation dialog
            // For now, we'll log the action and return true
            _logger.LogInformation("User confirmed posting order {OrderId} with total {Total}", 
                order.Id, order.TotalAmountWithTax);
            return true;
        }

        private async Task<Invoice?> CreateInvoiceAsync(SalesOrder order)
        {
            try
            {
                // Generate invoice number
                var invoiceNumber = await GenerateInvoiceNumberAsync();

                // Create invoice entity using correct constructor
                var invoice = new Invoice(
                    salesOrderId: order.Id,
                    invoiceNumber: invoiceNumber,
                    invoiceDate: DateTime.UtcNow,
                    dueDate: DateTime.UtcNow.AddDays(30) // 30 days payment terms
                );

                // Add invoice lines using correct method signature
                foreach (var orderLine in order.Lines)
                {
                    invoice.AddLine(
                        productId: orderLine.ProductId,
                        productDescription: $"Product {orderLine.ProductId}", // Simplified
                        quantity: orderLine.Quantity,
                        unitPrice: orderLine.UnitPrice,
                        taxRate: 0.10m // 10% tax rate
                    );
                }

                // Post the invoice
                invoice.Post();

                return invoice;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to create invoice for order {OrderId}", order.Id);
                return null;
            }
        }

        private async Task<string> GenerateInvoiceNumberAsync()
        {
            // Simple invoice number generation
            var timestamp = DateTime.UtcNow.ToString("yyyyMMdd-HHmmss");
            return $"INV-{timestamp}";
        }
    }

    public class PostingResult
    {
        public bool Success { get; set; }
        public string ErrorMessage { get; set; } = string.Empty;
        public int? InvoiceId { get; set; }
        public string InvoiceNumber { get; set; } = string.Empty;
        public DateTime PostedAt { get; set; }
        public string AuditTrailEntry { get; set; } = string.Empty;
    }
}
