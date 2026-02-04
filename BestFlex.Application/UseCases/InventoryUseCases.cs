using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using BestFlex.Application.Abstractions;
using BestFlex.Domain.Entities;
using BestFlex.Domain.Exceptions;

namespace BestFlex.Application.UseCases
{
    public interface IReceiveStockUseCase
    {
        Task ExecuteAsync(ReceiveStockCommand command, CancellationToken cancellationToken = default);
    }

    public class ReceiveStockCommand
    {
        public int ProductId { get; set; }
        public decimal Quantity { get; set; }
        public decimal UnitCost { get; set; }
        public string ReferenceNumber { get; set; } = string.Empty;
        public string Notes { get; set; } = string.Empty;
    }

    public class ReceiveStockUseCase : IReceiveStockUseCase
    {
        private readonly IUnitOfWork _unitOfWork;

        public ReceiveStockUseCase(IUnitOfWork unitOfWork)
        {
            _unitOfWork = unitOfWork ?? throw new ArgumentNullException(nameof(unitOfWork));
        }

        public async Task ExecuteAsync(ReceiveStockCommand command, CancellationToken cancellationToken = default)
        {
            await _unitOfWork.BeginAsync();

            try
            {
                // Validate quantity
                if (command.Quantity <= 0)
                    throw new DomainException("Received quantity must be positive");

                // Validate unit cost
                if (command.UnitCost < 0)
                    throw new DomainException("Unit cost cannot be negative");

                // In Phase 4A, we would:
                // 1. Lock stock row to prevent concurrent updates
                // 2. Get existing stock or create new
                // 3. Increase stock quantity
                // 4. Create stock movement
                // 5. Create accounting entries

                // For now, just commit the transaction to demonstrate ACID behavior
                await _unitOfWork.CommitAsync();
            }
            catch
            {
                await _unitOfWork.RollbackAsync();
                throw;
            }
        }
    }

    public interface IAdjustStockUseCase
    {
        Task ExecuteAsync(AdjustStockCommand command, CancellationToken cancellationToken = default);
    }

    public class AdjustStockCommand
    {
        public int ProductId { get; set; }
        public decimal Quantity { get; set; }
        public string MovementType { get; set; } = "OUT"; // IN, OUT, ADJUST
        public string Reason { get; set; } = string.Empty;
        public int ManagerId { get; set; } // Manager approval required
        public string ReferenceNumber { get; set; } = string.Empty;
    }

    public class AdjustStockUseCase : IAdjustStockUseCase
    {
        private readonly IUnitOfWork _unitOfWork;

        public AdjustStockUseCase(IUnitOfWork unitOfWork)
        {
            _unitOfWork = unitOfWork ?? throw new ArgumentNullException(nameof(unitOfWork));
        }

        public async Task ExecuteAsync(AdjustStockCommand command, CancellationToken cancellationToken = default)
        {
            await _unitOfWork.BeginAsync();

            try
            {
                // Validate manager ID (in real system, would check if user is manager)
                if (command.ManagerId <= 0)
                    throw new DomainException("Manager approval required for stock adjustments");

                // Validate quantity
                if (command.Quantity <= 0)
                    throw new DomainException("Adjustment quantity must be positive");

                // Validate movement type
                if (command.MovementType != "IN" && command.MovementType != "OUT" && command.MovementType != "ADJUST")
                    throw new DomainException("Invalid movement type. Must be IN, OUT, or ADJUST");

                // In Phase 4A, we would:
                // 1. Lock stock row to prevent concurrent updates
                // 2. Check available quantity for OUT movements
                // 3. Prevent negative stock
                // 4. Perform adjustment
                // 5. Create stock movement
                // 6. Create accounting entries

                // For now, just commit the transaction to demonstrate ACID behavior
                await _unitOfWork.CommitAsync();
            }
            catch
            {
                await _unitOfWork.RollbackAsync();
                throw;
            }
        }
    }

    public interface IReserveStockUseCase
    {
        Task<string> ExecuteAsync(ReserveStockCommand command, CancellationToken cancellationToken = default);
    }

    public class ReserveStockCommand
    {
        public int ProductId { get; set; }
        public decimal Quantity { get; set; }
        public string OrderNumber { get; set; } = string.Empty;
        public DateTime ExpiresAt { get; set; }
    }

    public class ReserveStockUseCase : IReserveStockUseCase
    {
        private readonly IUnitOfWork _unitOfWork;

        public ReserveStockUseCase(IUnitOfWork unitOfWork)
        {
            _unitOfWork = unitOfWork ?? throw new ArgumentNullException(nameof(unitOfWork));
        }

        public async Task<string> ExecuteAsync(ReserveStockCommand command, CancellationToken cancellationToken = default)
        {
            await _unitOfWork.BeginAsync();

            try
            {
                // Validate quantity
                if (command.Quantity <= 0)
                    throw new DomainException("Reservation quantity must be positive");

                // In Phase 4A, we would:
                // 1. Lock stock row to prevent concurrent updates
                // 2. Check available quantity
                // 3. Prevent over-reservation
                // 4. Create reservation record
                // 5. Update available quantity

                // For now, just commit the transaction and return dummy reservation ID
                await _unitOfWork.CommitAsync();

                return Guid.NewGuid().ToString();
            }
            catch
            {
                await _unitOfWork.RollbackAsync();
                throw;
            }
        }
    }

    // Helper classes for Phase 4A (would be in domain layer)
    public class StockMovement
    {
        public int ProductId { get; set; }
        public string MovementType { get; set; }
        public decimal Quantity { get; set; }
        public decimal UnitCost { get; set; }
        public string ReferenceNumber { get; set; }
        public string Notes { get; set; }

        public StockMovement(int productId, string movementType, decimal quantity, decimal unitCost, string referenceNumber, string notes)
        {
            ProductId = productId;
            MovementType = movementType;
            Quantity = quantity;
            UnitCost = unitCost;
            ReferenceNumber = referenceNumber;
            Notes = notes;
        }
    }

    public class StockReservation
    {
        public string ReservationId { get; set; }
        public int ProductId { get; set; }
        public decimal Quantity { get; set; }
        public string OrderNumber { get; set; }
        public DateTime ExpiresAt { get; set; }

        public StockReservation(string reservationId, int productId, decimal quantity, string orderNumber, DateTime expiresAt)
        {
            ReservationId = reservationId;
            ProductId = productId;
            Quantity = quantity;
            OrderNumber = orderNumber;
            ExpiresAt = expiresAt;
        }
    }
}
