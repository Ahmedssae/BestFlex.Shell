using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace BestFlex.Shell.Services
{
    public class InventoryReadService : IInventoryReadService
    {
        private readonly ILogger<InventoryReadService> _logger;

        public InventoryReadService(ILogger<InventoryReadService> logger)
        {
            _logger = logger;
        }

        public async Task<InventoryInfo> GetInventoryInfoAsync(int productId)
        {
            try
            {
                // Simulate inventory lookup - in real implementation this would query database
                await Task.Delay(50); // Simulate network latency

                // Mock inventory data for demonstration
                var inventory = GetMockInventoryData(productId);
                
                _logger.LogDebug("Retrieved inventory for Product {ProductId}: {Available}", 
                    productId, inventory.AvailableQuantity);
                
                return inventory;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to get inventory info for Product {ProductId}", productId);
                return new InventoryInfo 
                { 
                    ProductId = productId,
                    ProductName = "Unknown Product",
                    AvailableQuantity = 0,
                    IsAvailable = false,
                    Status = "Error"
                };
            }
        }

        public async Task<List<InventoryInfo>> GetInventoryInfoAsync(List<int> productIds)
        {
            try
            {
                var tasks = productIds.Select(id => GetInventoryInfoAsync(id));
                var results = await Task.WhenAll(tasks);
                
                _logger.LogDebug("Retrieved inventory for {Count} products", productIds.Count);
                
                return results.ToList();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to get inventory info for multiple products");
                return productIds.Select(id => new InventoryInfo 
                { 
                    ProductId = id,
                    ProductName = "Unknown Product",
                    AvailableQuantity = 0,
                    IsAvailable = false,
                    Status = "Error"
                }).ToList();
            }
        }

        private InventoryInfo GetMockInventoryData(int productId)
        {
            // Mock inventory data - in real implementation this would query actual inventory
            var mockData = new Dictionary<int, InventoryInfo>
            {
                { 1, new InventoryInfo { ProductId = 1, ProductName = "Widget A", AvailableQuantity = 100, IsAvailable = true, Status = "In Stock" } },
                { 2, new InventoryInfo { ProductId = 2, ProductName = "Widget B", AvailableQuantity = 25, IsAvailable = true, Status = "Low Stock" } },
                { 3, new InventoryInfo { ProductId = 3, ProductName = "Widget C", AvailableQuantity = 0, IsAvailable = false, Status = "Out of Stock" } },
                { 4, new InventoryInfo { ProductId = 4, ProductName = "Widget D", AvailableQuantity = 500, IsAvailable = true, Status = "In Stock" } },
                { 5, new InventoryInfo { ProductId = 5, ProductName = "Widget E", AvailableQuantity = 5, IsAvailable = true, Status = "Very Low" } }
            };

            return mockData.TryGetValue(productId, out var info) 
                ? info 
                : new InventoryInfo 
                { 
                    ProductId = productId, 
                    ProductName = $"Product {productId}", 
                    AvailableQuantity = 0, 
                    IsAvailable = false, 
                    Status = "Unknown" 
                };
        }
    }
}
