using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using BestFlex.Application.Abstractions;
using BestFlex.Application.Abstractions.Contracts.Sales;

namespace BestFlex.Application.Services
{
    public class StockValidationService : IStockValidationService
    {
        public Task<StockValidationResult> ValidateStockAsync(IEnumerable<NewSaleItemDto> items, CancellationToken cancellationToken = default)
        {
            // Minimal implementation - always valid
            return Task.FromResult(new StockValidationResult { IsValid = true });
        }

        public Task<StockReservationResult> ReserveStockAsync(IEnumerable<NewSaleItemDto> items, CancellationToken cancellationToken = default)
        {
            // Minimal implementation - always succeeds
            return Task.FromResult(new StockReservationResult 
            { 
                IsSuccess = true, 
                ReservationId = "dummy-reservation" 
            });
        }

        public Task ReleaseStockReservationAsync(string reservationId, CancellationToken cancellationToken = default)
        {
            // Minimal implementation - do nothing
            return Task.CompletedTask;
        }
    }
}
