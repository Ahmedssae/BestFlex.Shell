using MediatR;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using BestFlex.Domain.Entities;
using BestFlex.Domain.Exceptions;

namespace BestFlex.Application.UseCases.SalesOrders
{
    public class SaveSalesOrderDraftCommand : IRequest<SaveSalesOrderDraftResult>
    {
        public int? DraftId { get; set; }
        public string CustomerName { get; set; } = string.Empty;
        public DateTime OrderDate { get; set; }
        public List<SalesOrderLineDto> Lines { get; set; } = new();

        public class SalesOrderLineDto
        {
            public string Description { get; set; } = string.Empty;
            public decimal Quantity { get; set; }
            public decimal UnitPrice { get; set; }
            public decimal Discount { get; set; }
        }
    }

    public class SaveSalesOrderDraftResult
    {
        public bool Success { get; set; }
        public int? DraftId { get; set; }
        public string OrderNumber { get; set; } = string.Empty;
        public List<string> ValidationErrors { get; set; } = new();
        public DateTime SavedAt { get; set; }
    }

    public class SaveSalesOrderDraftCommandHandler : IRequestHandler<SaveSalesOrderDraftCommand, SaveSalesOrderDraftResult>
    {
        private readonly ISalesOrderRepository _salesOrderRepository;

        public SaveSalesOrderDraftCommandHandler(ISalesOrderRepository salesOrderRepository)
        {
            _salesOrderRepository = salesOrderRepository ?? throw new ArgumentNullException(nameof(salesOrderRepository));
        }

        public async Task<SaveSalesOrderDraftResult> Handle(SaveSalesOrderDraftCommand request, CancellationToken cancellationToken)
        {
            var result = new SaveSalesOrderDraftResult { SavedAt = DateTime.UtcNow };

            try
            {
                // Validate customer
                if (string.IsNullOrWhiteSpace(request.CustomerName))
                {
                    result.ValidationErrors.Add("Customer name is required");
                }

                // Validate lines if any exist
                foreach (var line in request.Lines)
                {
                    if (line.Quantity <= 0)
                    {
                        result.ValidationErrors.Add($"Quantity must be greater than 0 for item: {line.Description}");
                    }

                    if (line.UnitPrice < 0)
                    {
                        result.ValidationErrors.Add($"Unit price cannot be negative for item: {line.Description}");
                    }
                }

                // If validation errors exist, return without saving
                if (result.ValidationErrors.Count > 0)
                {
                    return result;
                }

                // Create or update sales order
                SalesOrder? salesOrder;
                
                if (request.DraftId.HasValue)
                {
                    // Update existing draft
                    salesOrder = await _salesOrderRepository.GetByIdAsync(request.DraftId.Value, cancellationToken);
                    if (salesOrder == null)
                    {
                        result.ValidationErrors.Add("Draft not found");
                        return result;
                    }

                    if (salesOrder.Status != SalesOrderStatus.Draft)
                    {
                        result.ValidationErrors.Add("Only draft orders can be modified");
                        return result;
                    }

                    // Clear existing lines and add new ones
                    salesOrder.ClearLines();
                }
                else
                {
                    // Create new draft
                    var orderNumber = await GenerateOrderNumberAsync(cancellationToken);
                    salesOrder = new SalesOrder(1, orderNumber, request.OrderDate); // CustomerId = 1 for now
                }

                // Add lines
                foreach (var lineDto in request.Lines)
                {
                    salesOrder.AddLine(1, lineDto.Quantity, lineDto.UnitPrice, lineDto.Discount); // ProductId = 1 for now
                }

                // Save to database
                if (request.DraftId.HasValue)
                {
                    await _salesOrderRepository.UpdateAsync(salesOrder, cancellationToken);
                }
                else
                {
                    await _salesOrderRepository.AddAsync(salesOrder, cancellationToken);
                }

                result.Success = true;
                result.DraftId = salesOrder.Id;
                result.OrderNumber = salesOrder.OrderNumber;

                return result;
            }
            catch (DomainException ex)
            {
                result.ValidationErrors.Add(ex.Message);
                return result;
            }
            catch (Exception ex)
            {
                result.ValidationErrors.Add("Failed to save draft due to an unexpected error");
                // Log the exception - in real implementation, would use ILogger
                Console.WriteLine($"Error saving draft: {ex}");
                return result;
            }
        }

        private Task<string> GenerateOrderNumberAsync(CancellationToken cancellationToken)
        {
            // Simple order number generation - in real implementation would be more sophisticated
            var timestamp = DateTime.UtcNow.ToString("yyyyMMdd-HHmmss");
            var orderNumber = $"SO-{timestamp}";
            return Task.FromResult(orderNumber);
        }
    }
}
