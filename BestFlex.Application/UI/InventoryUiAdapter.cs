using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using BestFlex.Application.Abstractions;
using BestFlex.Application.UseCases;
using BestFlex.Domain.Exceptions;

namespace BestFlex.Application.UI
{
    // Invoice PDF Exporter
    public interface IInvoicePdfExporter
    {
        Task<byte[]> RenderPdfAsync(object data, CancellationToken cancellationToken = default);
    }

    // Payment UI Adapter
    public interface IPaymentUiAdapter
    {
        Task<PaymentRegistrationResult> RegisterPaymentAsync(PaymentRegistrationUiRequest request, CancellationToken cancellationToken = default);
    }

    // Payment Registration DTOs
    public class PaymentRegistrationUiRequest
    {
        public int InvoiceId { get; set; }
        public decimal Amount { get; set; }
        public string PaymentMethod { get; set; } = string.Empty;
        public string ReferenceNumber { get; set; } = string.Empty;
        public DateTime PaymentDate { get; set; }
        public string Notes { get; set; } = string.Empty;
    }

    public class PaymentRegistrationResult
    {
        public bool Success { get; set; }
        public int PaymentId { get; set; }
        public string ErrorMessage { get; set; } = string.Empty;
        public string UserFriendlyMessage { get; set; } = string.Empty;
        public List<PaymentValidationError> ValidationErrors { get; set; } = new();
    }

    public class PaymentValidationError
    {
        public string FieldName { get; set; } = string.Empty;
        public string ErrorMessage { get; set; } = string.Empty;
    }

    // Inventory UI Adapter - Safe bridge between UI and Application Services
    public interface IInventoryUiAdapter
    {
        Task<InventoryOverviewResult> GetInventoryOverviewAsync(CancellationToken cancellationToken = default);
        Task<InventoryCountResult> GetInventoryCountAsync(CancellationToken cancellationToken = default);
        Task<StockReceiptResult> ReceiveStockAsync(ReceiveStockUiRequest request, CancellationToken cancellationToken = default);
        Task<StockAdjustmentResult> AdjustStockAsync(AdjustStockUiRequest request, CancellationToken cancellationToken = default);
        Task<InventoryStockReservationResult> ReserveStockAsync(InventoryReserveStockUiRequest request, CancellationToken cancellationToken = default);
    }

    // UI Request DTOs
    public class ReceiveStockUiRequest
    {
        public int ProductId { get; set; }
        public decimal Quantity { get; set; }
        public decimal UnitCost { get; set; }
        public string ReferenceNumber { get; set; } = string.Empty;
        public string Notes { get; set; } = string.Empty;
    }

    public class AdjustStockUiRequest
    {
        public int ProductId { get; set; }
        public decimal Quantity { get; set; }
        public string MovementType { get; set; } = "OUT"; // IN, OUT, ADJUST
        public string Reason { get; set; } = string.Empty;
        public int ManagerId { get; set; } // Manager approval required
        public string ReferenceNumber { get; set; } = string.Empty;
    }

    public class InventoryReserveStockUiRequest
    {
        public int ProductId { get; set; }
        public decimal Quantity { get; set; }
        public string OrderNumber { get; set; } = string.Empty;
        public DateTime ExpiresAt { get; set; }
    }

    // UI Result DTOs
    public class InventoryOverviewResult
    {
        public bool Success { get; set; }
        public List<InventoryOverviewItemDto> InventoryItems { get; set; } = new();
        public string ErrorMessage { get; set; } = string.Empty;
        public string UserFriendlyMessage { get; set; } = string.Empty;
        public List<InventoryValidationError> ValidationErrors { get; set; } = new();
    }

    public class InventoryCountResult
    {
        public bool Success { get; set; }
        public int TotalCount { get; set; }
        public string ErrorMessage { get; set; } = string.Empty;
        public string UserFriendlyMessage { get; set; } = string.Empty;
        public List<InventoryValidationError> ValidationErrors { get; set; } = new();
    }

    public class InventoryOverviewItemDto
    {
        public int ProductId { get; set; }
        public string SKU { get; set; } = string.Empty;
        public string ProductName { get; set; } = string.Empty;
        public decimal TotalQuantity { get; set; }
        public decimal AvailableQuantity { get; set; }
        public decimal ReservedQuantity { get; set; }
        public decimal UnitCost { get; set; }
        public decimal TotalValue { get; set; }
        public string ValuationMethod { get; set; } = "FIFO";
        public bool IsActive { get; set; }
        public DateTime LastUpdated { get; set; }
    }

    public class StockReceiptResult
    {
        public bool Success { get; set; }
        public string ErrorMessage { get; set; } = string.Empty;
        public string UserFriendlyMessage { get; set; } = string.Empty;
        public List<InventoryValidationError> ValidationErrors { get; set; } = new();
        public decimal NewStockLevel { get; set; }
    }

    public class StockAdjustmentResult
    {
        public bool Success { get; set; }
        public string ErrorMessage { get; set; } = string.Empty;
        public string UserFriendlyMessage { get; set; } = string.Empty;
        public List<InventoryValidationError> ValidationErrors { get; set; } = new();
        public decimal NewStockLevel { get; set; }
    }

    public class InventoryStockReservationResult
    {
        public bool Success { get; set; }
        public string ReservationId { get; set; } = string.Empty;
        public string ErrorMessage { get; set; } = string.Empty;
        public string UserFriendlyMessage { get; set; } = string.Empty;
        public List<InventoryValidationError> ValidationErrors { get; set; } = new();
        public decimal AvailableAfterReservation { get; set; }
    }

    public class InventoryValidationError
    {
        public string PropertyName { get; set; } = string.Empty;
        public string ErrorMessage { get; set; } = string.Empty;
    }

    // Invoice PDF Exporter Implementation
    public class InvoicePdfExporter : IInvoicePdfExporter
    {
        public async Task<byte[]> RenderPdfAsync(object data, CancellationToken cancellationToken = default)
        {
            // TODO: Implement actual PDF generation
            await Task.Delay(100, cancellationToken);
            return Array.Empty<byte>();
        }
    }

    // Payment UI Adapter Implementation
    public class PaymentUiAdapter : IPaymentUiAdapter
    {
        public async Task<PaymentRegistrationResult> RegisterPaymentAsync(PaymentRegistrationUiRequest request, CancellationToken cancellationToken = default)
        {
            // TODO: Implement actual payment registration
            await Task.Delay(100, cancellationToken);
            return new PaymentRegistrationResult
            {
                Success = true,
                PaymentId = 1,
                UserFriendlyMessage = "Payment registered successfully"
            };
        }
    }

    // Implementation
    public class InventoryUiAdapter : IInventoryUiAdapter
    {
        private readonly IReceiveStockUseCase _receiveStockUseCase;
        private readonly IAdjustStockUseCase _adjustStockUseCase;
        private readonly IReserveStockUseCase _reserveStockUseCase;

        public InventoryUiAdapter(
            IReceiveStockUseCase receiveStockUseCase,
            IAdjustStockUseCase adjustStockUseCase,
            IReserveStockUseCase reserveStockUseCase)
        {
            _receiveStockUseCase = receiveStockUseCase ?? throw new ArgumentNullException(nameof(receiveStockUseCase));
            _adjustStockUseCase = adjustStockUseCase ?? throw new ArgumentNullException(nameof(adjustStockUseCase));
            _reserveStockUseCase = reserveStockUseCase ?? throw new ArgumentNullException(nameof(reserveStockUseCase));
        }

        public async Task<InventoryOverviewResult> GetInventoryOverviewAsync(CancellationToken cancellationToken = default)
        {
            try
            {
                // TODO: Implement actual inventory retrieval from database
                // For now, return mock data to demonstrate UI functionality
                var inventoryItems = new List<InventoryOverviewItemDto>
                {
                    new InventoryOverviewItemDto
                    {
                        ProductId = 1,
                        SKU = "PROD-001",
                        ProductName = "Sample Product 1",
                        TotalQuantity = 1000,
                        AvailableQuantity = 750,
                        ReservedQuantity = 250,
                        UnitCost = 50.00m,
                        TotalValue = 50000.00m,
                        ValuationMethod = "FIFO",
                        IsActive = true,
                        LastUpdated = DateTime.UtcNow.AddHours(-2)
                    },
                    new InventoryOverviewItemDto
                    {
                        ProductId = 2,
                        SKU = "PROD-002",
                        ProductName = "Sample Product 2",
                        TotalQuantity = 500,
                        AvailableQuantity = 450,
                        ReservedQuantity = 50,
                        UnitCost = 25.00m,
                        TotalValue = 12500.00m,
                        ValuationMethod = "AVCO",
                        IsActive = true,
                        LastUpdated = DateTime.UtcNow.AddHours(-1)
                    },
                    new InventoryOverviewItemDto
                    {
                        ProductId = 3,
                        SKU = "PROD-003",
                        ProductName = "Inactive Product",
                        TotalQuantity = 200,
                        AvailableQuantity = 200,
                        ReservedQuantity = 0,
                        UnitCost = 30.00m,
                        TotalValue = 6000.00m,
                        ValuationMethod = "FIFO",
                        IsActive = false,
                        LastUpdated = DateTime.UtcNow.AddDays(-1)
                    },
                    new InventoryOverviewItemDto
                    {
                        ProductId = 4,
                        SKU = "PROD-004",
                        ProductName = "High Demand Product",
                        TotalQuantity = 2000,
                        AvailableQuantity = 200,
                        ReservedQuantity = 1800,
                        UnitCost = 75.00m,
                        TotalValue = 150000.00m,
                        ValuationMethod = "FIFO",
                        IsActive = true,
                        LastUpdated = DateTime.UtcNow.AddMinutes(-30)
                    }
                };

                return new InventoryOverviewResult
                {
                    Success = true,
                    InventoryItems = inventoryItems,
                    UserFriendlyMessage = "Inventory overview loaded successfully"
                };
            }
            catch (Exception ex)
            {
                return new InventoryOverviewResult
                {
                    Success = false,
                    InventoryItems = new List<InventoryOverviewItemDto>(),
                    ErrorMessage = ex.Message,
                    UserFriendlyMessage = "Failed to load inventory overview"
                };
            }
        }

        public async Task<InventoryCountResult> GetInventoryCountAsync(CancellationToken cancellationToken = default)
        {
            try
            {
                // TODO: Implement actual inventory count retrieval from database
                // For now, return mock data to demonstrate UI functionality
                var inventoryResult = await GetInventoryOverviewAsync(cancellationToken);
                
                return new InventoryCountResult
                {
                    Success = inventoryResult.Success,
                    TotalCount = inventoryResult.InventoryItems.Count,
                    UserFriendlyMessage = "Inventory count retrieved successfully"
                };
            }
            catch (Exception ex)
            {
                return new InventoryCountResult
                {
                    Success = false,
                    TotalCount = 0,
                    ErrorMessage = ex.Message,
                    UserFriendlyMessage = "Failed to get inventory count"
                };
            }
        }

        public async Task<StockReceiptResult> ReceiveStockAsync(ReceiveStockUiRequest request, CancellationToken cancellationToken = default)
        {
            try
            {
                // Validate UI request
                var validationErrors = ValidateReceiveStockRequest(request);
                if (validationErrors.Any())
                {
                    return new StockReceiptResult
                    {
                        Success = false,
                        ValidationErrors = validationErrors,
                        UserFriendlyMessage = "Please correct the validation errors"
                    };
                }

                // Map UI request to application command
                var command = new ReceiveStockCommand
                {
                    ProductId = request.ProductId,
                    Quantity = request.Quantity,
                    UnitCost = request.UnitCost,
                    ReferenceNumber = request.ReferenceNumber,
                    Notes = request.Notes
                };

                // Call application service
                await _receiveStockUseCase.ExecuteAsync(command, cancellationToken);

                // In Phase 4A, we would return the actual new stock level
                // For now, return a dummy value
                return new StockReceiptResult
                {
                    Success = true,
                    NewStockLevel = 100, // Dummy value
                    UserFriendlyMessage = $"Successfully received {request.Quantity} units"
                };
            }
            catch (DomainException ex)
            {
                return new StockReceiptResult
                {
                    Success = false,
                    ErrorMessage = ex.Message,
                    UserFriendlyMessage = TranslateDomainException(ex),
                    ValidationErrors = ExtractValidationErrors(ex)
                };
            }
            catch (Exception ex)
            {
                return new StockReceiptResult
                {
                    Success = false,
                    ErrorMessage = ex.Message,
                    UserFriendlyMessage = "An unexpected error occurred while receiving stock"
                };
            }
        }

        public async Task<StockAdjustmentResult> AdjustStockAsync(AdjustStockUiRequest request, CancellationToken cancellationToken = default)
        {
            try
            {
                // Validate UI request
                var validationErrors = ValidateAdjustStockRequest(request);
                if (validationErrors.Any())
                {
                    return new StockAdjustmentResult
                    {
                        Success = false,
                        ValidationErrors = validationErrors,
                        UserFriendlyMessage = "Please correct the validation errors"
                    };
                }

                // Map UI request to application command
                var command = new AdjustStockCommand
                {
                    ProductId = request.ProductId,
                    Quantity = request.Quantity,
                    MovementType = request.MovementType,
                    Reason = request.Reason,
                    ManagerId = request.ManagerId,
                    ReferenceNumber = request.ReferenceNumber
                };

                // Call application service
                await _adjustStockUseCase.ExecuteAsync(command, cancellationToken);

                // In Phase 4A, we would return the actual new stock level
                // For now, return a dummy value
                return new StockAdjustmentResult
                {
                    Success = true,
                    NewStockLevel = 50, // Dummy value
                    UserFriendlyMessage = $"Stock adjustment completed successfully"
                };
            }
            catch (DomainException ex)
            {
                return new StockAdjustmentResult
                {
                    Success = false,
                    ErrorMessage = ex.Message,
                    UserFriendlyMessage = TranslateDomainException(ex),
                    ValidationErrors = ExtractValidationErrors(ex)
                };
            }
            catch (Exception ex)
            {
                return new StockAdjustmentResult
                {
                    Success = false,
                    ErrorMessage = ex.Message,
                    UserFriendlyMessage = "An unexpected error occurred while adjusting stock"
                };
            }
        }

        public async Task<InventoryStockReservationResult> ReserveStockAsync(InventoryReserveStockUiRequest request, CancellationToken cancellationToken = default)
        {
            try
            {
                // Validate UI request
                var validationErrors = ValidateReserveStockRequest(request);
                if (validationErrors.Any())
                {
                    return new InventoryStockReservationResult
                    {
                        Success = false,
                        ValidationErrors = validationErrors,
                        UserFriendlyMessage = "Please correct the validation errors"
                    };
                }

                // Map UI request to application command
                var command = new ReserveStockCommand
                {
                    ProductId = request.ProductId,
                    Quantity = request.Quantity,
                    OrderNumber = request.OrderNumber,
                    ExpiresAt = request.ExpiresAt
                };

                // Call application service
                var reservationId = await _reserveStockUseCase.ExecuteAsync(command, cancellationToken);

                // In Phase 4A, we would return the actual available quantity after reservation
                // For now, return a dummy value
                return new InventoryStockReservationResult
                {
                    Success = true,
                    ReservationId = reservationId,
                    AvailableAfterReservation = 25, // Dummy value
                    UserFriendlyMessage = $"Successfully reserved {request.Quantity} units"
                };
            }
            catch (DomainException ex)
            {
                return new InventoryStockReservationResult
                {
                    Success = false,
                    ErrorMessage = ex.Message,
                    UserFriendlyMessage = TranslateDomainException(ex),
                    ValidationErrors = ExtractValidationErrors(ex)
                };
            }
            catch (Exception ex)
            {
                return new InventoryStockReservationResult
                {
                    Success = false,
                    ErrorMessage = ex.Message,
                    UserFriendlyMessage = "An unexpected error occurred while reserving stock"
                };
            }
        }

        private static List<InventoryValidationError> ValidateReceiveStockRequest(ReceiveStockUiRequest request)
        {
            var errors = new List<InventoryValidationError>();

            if (request.ProductId <= 0)
                errors.Add(new InventoryValidationError { PropertyName = nameof(request.ProductId), ErrorMessage = "Valid product is required" });

            if (request.Quantity <= 0)
                errors.Add(new InventoryValidationError { PropertyName = nameof(request.Quantity), ErrorMessage = "Quantity must be positive" });

            if (request.UnitCost < 0)
                errors.Add(new InventoryValidationError { PropertyName = nameof(request.UnitCost), ErrorMessage = "Unit cost cannot be negative" });

            return errors;
        }

        private static List<InventoryValidationError> ValidateAdjustStockRequest(AdjustStockUiRequest request)
        {
            var errors = new List<InventoryValidationError>();

            if (request.ProductId <= 0)
                errors.Add(new InventoryValidationError { PropertyName = nameof(request.ProductId), ErrorMessage = "Valid product is required" });

            if (request.Quantity <= 0)
                errors.Add(new InventoryValidationError { PropertyName = nameof(request.Quantity), ErrorMessage = "Quantity must be positive" });

            if (string.IsNullOrWhiteSpace(request.MovementType))
                errors.Add(new InventoryValidationError { PropertyName = nameof(request.MovementType), ErrorMessage = "Movement type is required" });

            if (!new[] { "IN", "OUT", "ADJUST" }.Contains(request.MovementType.ToUpper()))
                errors.Add(new InventoryValidationError { PropertyName = nameof(request.MovementType), ErrorMessage = "Invalid movement type" });

            if (request.MovementType.ToUpper() == "ADJUST" && request.ManagerId <= 0)
                errors.Add(new InventoryValidationError { PropertyName = nameof(request.ManagerId), ErrorMessage = "Manager approval required for adjustments" });

            return errors;
        }

        private static List<InventoryValidationError> ValidateReserveStockRequest(InventoryReserveStockUiRequest request)
        {
            var errors = new List<InventoryValidationError>();

            if (request.ProductId <= 0)
                errors.Add(new InventoryValidationError { PropertyName = nameof(request.ProductId), ErrorMessage = "Valid product is required" });

            if (request.Quantity <= 0)
                errors.Add(new InventoryValidationError { PropertyName = nameof(request.Quantity), ErrorMessage = "Quantity must be positive" });

            if (request.ExpiresAt <= DateTime.UtcNow)
                errors.Add(new InventoryValidationError { PropertyName = nameof(request.ExpiresAt), ErrorMessage = "Expiration must be in the future" });

            return errors;
        }

        private static List<InventoryValidationError> ExtractValidationErrors(DomainException ex)
        {
            var errors = new List<InventoryValidationError>();

            if (ex.Message.Contains("Product"))
                errors.Add(new InventoryValidationError { PropertyName = "ProductId", ErrorMessage = "Invalid product" });

            if (ex.Message.Contains("Quantity"))
                errors.Add(new InventoryValidationError { PropertyName = "Quantity", ErrorMessage = "Invalid quantity" });

            if (ex.Message.Contains("Manager"))
                errors.Add(new InventoryValidationError { PropertyName = "ManagerId", ErrorMessage = "Manager approval required" });

            if (ex.Message.Contains("movement type"))
                errors.Add(new InventoryValidationError { PropertyName = "MovementType", ErrorMessage = "Invalid movement type" });

            return errors;
        }

        private static string TranslateDomainException(DomainException ex)
        {
            return ex.Message switch
            {
                var msg when msg.Contains("insufficient") => "Insufficient stock available",
                var msg when msg.Contains("negative") => "Stock cannot go negative",
                var msg when msg.Contains("Manager") => "Manager approval required for this operation",
                var msg when msg.Contains("Product") => "Please select a valid product",
                var msg when msg.Contains("Quantity") => "Please enter a valid quantity",
                var msg when msg.Contains("movement type") => "Please select a valid movement type",
                _ => "Please check the information entered and try again"
            };
        }
    }
}
