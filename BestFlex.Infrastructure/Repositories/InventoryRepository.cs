using BestFlex.Application.UseCases.SalesOrders;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace BestFlex.Infrastructure.Repositories
{
    public class InventoryRepository : IInventoryRepository
    {
        public InventoryRepository()
        {
        }

        public async Task<decimal> GetAvailableStockAsync(int productId, CancellationToken cancellationToken)
        {
            // Phase 5: Mock implementation - would query actual inventory
            // For demo purposes, assume we have sufficient stock
            await Task.Delay(10, cancellationToken); // Simulate DB call
            return 10000m; // Always return sufficient stock for demo
        }

        public async Task<decimal> GetUnitCostAsync(int productId, decimal quantity, CancellationToken cancellationToken)
        {
            // Phase 5: Mock implementation - would calculate FIFO/AVCO cost
            await Task.Delay(10, cancellationToken); // Simulate DB call
            return 25.00m; // Fixed cost for demo
        }

        public async Task DeductStockAsync(int productId, decimal quantity, decimal unitCost, CancellationToken cancellationToken)
        {
            // Phase 5: Mock implementation - would create stock movements
            await Task.Delay(10, cancellationToken); // Simulate DB call
            
            // In real implementation:
            // 1. Create StockMovement record
            // 2. Update ProductStock table
            // 3. Handle concurrency with row versioning
            // 4. Throw exception if insufficient stock
            
            // For demo, just simulate success
            Console.WriteLine($"Deducted {quantity} units of product {productId} at {unitCost:C} each");
        }
    }
}
