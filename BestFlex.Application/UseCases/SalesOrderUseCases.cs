using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using BestFlex.Application.Abstractions;
using BestFlex.Domain.Entities;
using BestFlex.Domain.Exceptions;

namespace BestFlex.Application.UseCases
{
    public interface ICreateSalesOrderUseCase
    {
        Task<int> ExecuteAsync(CreateSalesOrderCommand command, CancellationToken cancellationToken = default);
    }

    public class CreateSalesOrderCommand
    {
        public int CustomerId { get; set; }
        public DateTime OrderDate { get; set; }
        public List<SalesOrderLineCommand> Lines { get; set; } = new();
        public string Notes { get; set; } = string.Empty;
        public string Currency { get; set; } = "USD";
    }

    public class SalesOrderLineCommand
    {
        public int ProductId { get; set; }
        public decimal Quantity { get; set; }
        public decimal UnitPrice { get; set; }
        public decimal Discount { get; set; }
    }

    public class CreateSalesOrderUseCase : ICreateSalesOrderUseCase
    {
        private readonly IUnitOfWork _unitOfWork;

        public CreateSalesOrderUseCase(IUnitOfWork unitOfWork)
        {
            _unitOfWork = unitOfWork ?? throw new ArgumentNullException(nameof(unitOfWork));
        }

        public async Task<int> ExecuteAsync(CreateSalesOrderCommand command, CancellationToken cancellationToken = default)
        {
            await _unitOfWork.BeginAsync();

            try
            {
                // Validate basic inputs
                if (command.CustomerId <= 0)
                    throw new DomainException("Customer ID is required");

                if (command.Lines == null || !command.Lines.Any())
                    throw new DomainException("Order must have at least one line");

                // Validate each line
                foreach (var line in command.Lines)
                {
                    if (line.ProductId <= 0)
                        throw new DomainException("Product ID is required");
                    
                    if (line.Quantity <= 0)
                        throw new DomainException("Quantity must be positive");
                    
                    if (line.UnitPrice <= 0)
                        throw new DomainException("Unit price must be positive");
                    
                    if (line.Discount < 0 || line.Discount > 100)
                        throw new DomainException("Discount must be between 0 and 100");
                }

                // Generate order number (in real implementation, would be sequential)
                var orderNumber = GenerateOrderNumber();
                
                // Create sales order (domain validation happens here)
                var salesOrder = new SalesOrder(command.CustomerId, orderNumber, command.OrderDate);

                // Add lines to sales order (domain validation happens here)
                foreach (var line in command.Lines)
                {
                    salesOrder.AddLine(line.ProductId, line.Quantity, line.UnitPrice, line.Discount);
                }

                // Update notes if provided
                if (!string.IsNullOrWhiteSpace(command.Notes))
                {
                    salesOrder.UpdateNotes(command.Notes);
                }

                // Confirm the order (domain validation happens here)
                salesOrder.Confirm();

                // In Phase 5, we would:
                // - Save sales order and lines to database
                // - Reserve stock for each line
                // - Update customer credit usage
                // - Create accounting entries
                // - Create audit trail entry

                // Commit transaction
                await _unitOfWork.CommitAsync();

                // Return a mock order ID for now
                return new Random().Next(1000, 9999);
            }
            catch
            {
                await _unitOfWork.RollbackAsync();
                throw;
            }
        }

        private string GenerateOrderNumber()
        {
            // Simplified order number generation
            return $"SO-{DateTime.UtcNow:yyyyMMdd}-{new Random().Next(1000, 9999)}";
        }
    }

    public interface ICancelSalesOrderUseCase
    {
        Task ExecuteAsync(CancelSalesOrderCommand command, CancellationToken cancellationToken = default);
    }

    public class CancelSalesOrderCommand
    {
        public int OrderId { get; set; }
        public string Reason { get; set; } = string.Empty;
        public int CancelledByUserId { get; set; }
    }

    public class CancelSalesOrderUseCase : ICancelSalesOrderUseCase
    {
        private readonly IUnitOfWork _unitOfWork;

        public CancelSalesOrderUseCase(IUnitOfWork unitOfWork)
        {
            _unitOfWork = unitOfWork ?? throw new ArgumentNullException(nameof(unitOfWork));
        }

        public async Task ExecuteAsync(CancelSalesOrderCommand command, CancellationToken cancellationToken = default)
        {
            await _unitOfWork.BeginAsync();

            try
            {
                // Validate inputs
                if (command.OrderId <= 0)
                    throw new DomainException("Order ID is required");

                if (command.CancelledByUserId <= 0)
                    throw new DomainException("Cancelled by user ID is required");

                // In Phase 5, we would:
                // 1. Load sales order with lines
                // 2. Validate order can be cancelled (not already shipped/invoiced)
                // 3. Release all stock reservations
                // 4. Update order status to Cancelled
                // 5. Create audit trail entry
                // 6. Create accounting entries (reverse any accruals)
                // 7. Commit transaction

                // For now, demonstrate the transaction pattern
                // Create a sales order instance to test domain rules
                var salesOrder = new SalesOrder(1, "TEST-001", DateTime.UtcNow);
                
                // Add a test line
                salesOrder.AddLine(1, 10, 100, 0);
                
                // Confirm the order first
                salesOrder.Confirm();

                // Cancel the order (domain validation happens here)
                // In the real domain, we would have a Cancel method
                // For now, just demonstrate the transaction pattern

                // Commit transaction
                await _unitOfWork.CommitAsync();
            }
            catch
            {
                await _unitOfWork.RollbackAsync();
                throw;
            }
        }
    }

    public interface IReserveStockForOrderUseCase
    {
        Task<List<string>> ExecuteAsync(ReserveStockForOrderCommand command, CancellationToken cancellationToken = default);
    }

    public class ReserveStockForOrderCommand
    {
        public int OrderId { get; set; }
        public List<StockReservationCommand> Reservations { get; set; } = new();
        public DateTime ExpiresAt { get; set; }
    }

    public class StockReservationCommand
    {
        public int ProductId { get; set; }
        public decimal Quantity { get; set; }
    }

    public class ReserveStockForOrderUseCase : IReserveStockForOrderUseCase
    {
        private readonly IUnitOfWork _unitOfWork;

        public ReserveStockForOrderUseCase(IUnitOfWork unitOfWork)
        {
            _unitOfWork = unitOfWork ?? throw new ArgumentNullException(nameof(unitOfWork));
        }

        public async Task<List<string>> ExecuteAsync(ReserveStockForOrderCommand command, CancellationToken cancellationToken = default)
        {
            await _unitOfWork.BeginAsync();

            try
            {
                var reservationIds = new List<string>();

                // In Phase 5, we would:
                // 1. Lock all stock rows for the products
                // 2. Check available quantity for each product
                // 3. Prevent overselling under concurrent orders
                // 4. Create reservations atomically
                // 5. Update available quantities
                // 6. Commit transaction

                foreach (var reservation in command.Reservations)
                {
                    // Validate reservation
                    if (reservation.ProductId <= 0)
                        throw new DomainException("Product ID is required");
                    
                    if (reservation.Quantity <= 0)
                        throw new DomainException("Reservation quantity must be positive");

                    // In Phase 5, we would:
                    // - Lock stock row
                    // - Check available quantity
                    // - Create reservation
                    // - Update available quantity

                    // For now, just generate a reservation ID
                    var reservationId = Guid.NewGuid().ToString();
                    reservationIds.Add(reservationId);
                }

                // Commit transaction
                await _unitOfWork.CommitAsync();

                return reservationIds;
            }
            catch
            {
                await _unitOfWork.RollbackAsync();
                throw;
            }
        }
    }

    public interface ICheckCreditLimitUseCase
    {
        Task ExecuteAsync(CheckCreditLimitCommand command, CancellationToken cancellationToken = default);
    }

    public class CheckCreditLimitCommand
    {
        public int CustomerId { get; set; }
        public decimal OrderAmount { get; set; }
    }

    public class CheckCreditLimitUseCase : ICheckCreditLimitUseCase
    {
        private readonly IUnitOfWork _unitOfWork;

        public CheckCreditLimitUseCase(IUnitOfWork unitOfWork)
        {
            _unitOfWork = unitOfWork ?? throw new ArgumentNullException(nameof(unitOfWork));
        }

        public async Task ExecuteAsync(CheckCreditLimitCommand command, CancellationToken cancellationToken = default)
        {
            await _unitOfWork.BeginAsync();

            try
            {
                // Validate inputs
                if (command.CustomerId <= 0)
                    throw new DomainException("Customer ID is required");

                if (command.OrderAmount < 0)
                    throw new DomainException("Order amount cannot be negative");

                // In Phase 5, we would:
                // 1. Load customer with current balance
                // 2. Calculate total outstanding orders
                // 3. Check if new order would exceed credit limit
                // 4. Throw CreditLimitExceededException if needed

                // For now, demonstrate the validation pattern
                if (command.OrderAmount > 10000) // Dummy credit limit check
                    throw new CreditLimitExceededException($"Order amount {command.OrderAmount} exceeds credit limit");

                // Commit transaction (read-only transaction)
                await _unitOfWork.CommitAsync();
            }
            catch
            {
                await _unitOfWork.RollbackAsync();
                throw;
            }
        }
    }
}
