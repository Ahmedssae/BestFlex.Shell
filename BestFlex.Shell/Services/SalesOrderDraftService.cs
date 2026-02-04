using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using BestFlex.Domain.Entities;

namespace BestFlex.Shell.Services
{
    public class SalesOrderDraftService
    {
        private readonly ISalesOrderRepository _repository;
        private readonly ILogger<SalesOrderDraftService> _logger;

        public SalesOrderDraftService(ISalesOrderRepository repository, ILogger<SalesOrderDraftService> logger)
        {
            _repository = repository;
            _logger = logger;
        }

        public async Task<SalesOrder?> CreateDraftAsync(string customerName, DateTime orderDate, List<SalesOrderLineDraft> lines)
        {
            try
            {
                // Generate order number
                var orderNumber = await GenerateOrderNumberAsync();

                // Create new SalesOrder using domain entity constructor
                var salesOrder = new SalesOrder(
                    customerId: 1, // TODO: Get from customer name lookup
                    orderNumber: orderNumber,
                    orderDate: orderDate
                );

                // Add lines using domain methods
                foreach (var line in lines)
                {
                    salesOrder.AddLine(
                        productId: 1, // TODO: Get from product description lookup
                        quantity: line.Quantity,
                        unitPrice: line.UnitPrice
                    );
                }

                var result = await _repository.SaveDraftAsync(salesOrder);
                _logger.LogInformation("Created draft {OrderNumber}", orderNumber);
                return result;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to create draft");
                return null;
            }
        }

        public async Task<SalesOrder?> UpdateDraftAsync(int id, string customerName, DateTime orderDate, List<SalesOrderLineDraft> lines)
        {
            try
            {
                var existing = await _repository.LoadDraftAsync(id);
                if (existing == null)
                {
                    _logger.LogWarning("Draft not found for update: {Id}", id);
                    return null;
                }

                // For simplicity, we'll create a new draft and replace the old one
                // In a real implementation, you'd use domain methods to update the existing entity
                var newOrder = await CreateDraftAsync(customerName, orderDate, lines);
                
                if (newOrder != null)
                {
                    // Delete the old draft
                    await _repository.DeleteDraftAsync(id);
                    _logger.LogInformation("Updated draft {OrderNumber}", newOrder.OrderNumber);
                }

                return newOrder;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to update draft {Id}", id);
                return null;
            }
        }

        public async Task<SalesOrder?> LoadDraftAsync(int id)
        {
            try
            {
                var draft = await _repository.LoadDraftAsync(id);
                if (draft != null)
                {
                    _logger.LogInformation("Loaded draft {OrderNumber}", draft.OrderNumber);
                }
                return draft;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to load draft {Id}", id);
                return null;
            }
        }

        public async Task<bool> DeleteDraftAsync(int id)
        {
            try
            {
                var result = await _repository.DeleteDraftAsync(id);
                if (result)
                {
                    _logger.LogInformation("Deleted draft {Id}", id);
                }
                return result;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to delete draft {Id}", id);
                return false;
            }
        }

        public async Task<List<SalesOrder>> GetDraftsAsync()
        {
            try
            {
                var drafts = await _repository.GetDraftsAsync();
                _logger.LogInformation("Loaded {Count} drafts", drafts.Count);
                return drafts;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to load drafts");
                return new List<SalesOrder>();
            }
        }

        private async Task<string> GenerateOrderNumberAsync()
        {
            // Simple order number generation - in real implementation this would be more sophisticated
            var timestamp = DateTime.UtcNow.ToString("yyyyMMdd-HHmmss");
            return $"SO-{timestamp}";
        }
    }
}
