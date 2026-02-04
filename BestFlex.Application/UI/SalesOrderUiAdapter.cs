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
    // Sales Order UI Adapter - Safe bridge between UI and Application Services
    public interface ISalesOrderUiAdapter
    {
        Task<SalesOrderCreationResult> CreateSalesOrderAsync(CreateSalesOrderUiRequest request, CancellationToken cancellationToken = default);
        Task<SalesOrderCancellationResult> CancelSalesOrderAsync(CancelSalesOrderUiRequest request, CancellationToken cancellationToken = default);
        Task<StockReservationResult> ReserveStockAsync(ReserveStockUiRequest request, CancellationToken cancellationToken = default);
        Task<CreditCheckResult> CheckCreditLimitAsync(CheckCreditUiRequest request, CancellationToken cancellationToken = default);
    }

    // UI Request DTOs
    public class CreateSalesOrderUiRequest
    {
        public int CustomerId { get; set; }
        public DateTime OrderDate { get; set; }
        public DateTime DeliveryDate { get; set; }
        public List<SalesOrderLineUiRequest> Lines { get; set; } = new();
        public string Notes { get; set; } = string.Empty;
        public string Currency { get; set; } = "USD";
    }

    public class SalesOrderLineUiRequest
    {
        public int ProductId { get; set; }
        public decimal Quantity { get; set; }
        public decimal UnitPrice { get; set; }
        public decimal Discount { get; set; }
    }

    public class CancelSalesOrderUiRequest
    {
        public int OrderId { get; set; }
        public string Reason { get; set; } = string.Empty;
        public int CancelledByUserId { get; set; }
    }

    public class ReserveStockUiRequest
    {
        public int OrderId { get; set; }
        public List<StockReservationUiRequest> Reservations { get; set; } = new();
        public DateTime ExpiresAt { get; set; }
    }

    public class StockReservationUiRequest
    {
        public int ProductId { get; set; }
        public decimal Quantity { get; set; }
    }

    public class CheckCreditUiRequest
    {
        public int CustomerId { get; set; }
        public decimal OrderAmount { get; set; }
    }

    // UI Result DTOs
    public class SalesOrderCreationResult
    {
        public bool Success { get; set; }
        public int OrderId { get; set; }
        public string ErrorMessage { get; set; } = string.Empty;
        public string UserFriendlyMessage { get; set; } = string.Empty;
        public List<SalesOrderValidationError> ValidationErrors { get; set; } = new();
    }

    public class SalesOrderCancellationResult
    {
        public bool Success { get; set; }
        public string ErrorMessage { get; set; } = string.Empty;
        public string UserFriendlyMessage { get; set; } = string.Empty;
        public List<SalesOrderValidationError> ValidationErrors { get; set; } = new();
    }

    public class StockReservationResult
    {
        public bool Success { get; set; }
        public List<string> ReservationIds { get; set; } = new();
        public string ErrorMessage { get; set; } = string.Empty;
        public string UserFriendlyMessage { get; set; } = string.Empty;
        public List<SalesOrderValidationError> ValidationErrors { get; set; } = new();
    }

    public class CreditCheckResult
    {
        public bool Success { get; set; }
        public string ErrorMessage { get; set; } = string.Empty;
        public string UserFriendlyMessage { get; set; } = string.Empty;
        public List<SalesOrderValidationError> ValidationErrors { get; set; } = new();
    }

    public class SalesOrderValidationError
    {
        public string PropertyName { get; set; } = string.Empty;
        public string ErrorMessage { get; set; } = string.Empty;
    }

    // Implementation
    public class SalesOrderUiAdapter : ISalesOrderUiAdapter
    {
        private readonly ICreateSalesOrderUseCase _createSalesOrderUseCase;
        private readonly ICancelSalesOrderUseCase _cancelSalesOrderUseCase;
        private readonly IReserveStockForOrderUseCase _reserveStockForOrderUseCase;
        private readonly ICheckCreditLimitUseCase _checkCreditLimitUseCase;

        public SalesOrderUiAdapter(
            ICreateSalesOrderUseCase createSalesOrderUseCase,
            ICancelSalesOrderUseCase cancelSalesOrderUseCase,
            IReserveStockForOrderUseCase reserveStockForOrderUseCase,
            ICheckCreditLimitUseCase checkCreditLimitUseCase)
        {
            _createSalesOrderUseCase = createSalesOrderUseCase ?? throw new ArgumentNullException(nameof(createSalesOrderUseCase));
            _cancelSalesOrderUseCase = cancelSalesOrderUseCase ?? throw new ArgumentNullException(nameof(cancelSalesOrderUseCase));
            _reserveStockForOrderUseCase = reserveStockForOrderUseCase ?? throw new ArgumentNullException(nameof(reserveStockForOrderUseCase));
            _checkCreditLimitUseCase = checkCreditLimitUseCase ?? throw new ArgumentNullException(nameof(checkCreditLimitUseCase));
        }

        public async Task<SalesOrderCreationResult> CreateSalesOrderAsync(CreateSalesOrderUiRequest request, CancellationToken cancellationToken = default)
        {
            try
            {
                // Validate UI request
                var validationErrors = ValidateCreateSalesOrderRequest(request);
                if (validationErrors.Any())
                {
                    return new SalesOrderCreationResult
                    {
                        Success = false,
                        ValidationErrors = validationErrors,
                        UserFriendlyMessage = "Please correct the validation errors"
                    };
                }

                // Map UI request to application command
                var command = new CreateSalesOrderCommand
                {
                    CustomerId = request.CustomerId,
                    OrderDate = request.OrderDate,
                    Lines = request.Lines.Select(l => new SalesOrderLineCommand
                    {
                        ProductId = l.ProductId,
                        Quantity = l.Quantity,
                        UnitPrice = l.UnitPrice,
                        Discount = l.Discount
                    }).ToList(),
                    Notes = request.Notes,
                    Currency = request.Currency
                };

                // Call application service
                var orderId = await _createSalesOrderUseCase.ExecuteAsync(command, cancellationToken);

                return new SalesOrderCreationResult
                {
                    Success = true,
                    OrderId = orderId,
                    UserFriendlyMessage = "Sales order created successfully"
                };
            }
            catch (CreditLimitExceededException ex)
            {
                return new SalesOrderCreationResult
                {
                    Success = false,
                    ErrorMessage = ex.Message,
                    UserFriendlyMessage = "Order exceeds customer credit limit"
                };
            }
            catch (InsufficientStockException ex)
            {
                return new SalesOrderCreationResult
                {
                    Success = false,
                    ErrorMessage = ex.Message,
                    UserFriendlyMessage = "Insufficient stock for one or more items"
                };
            }
            catch (DomainException ex)
            {
                return new SalesOrderCreationResult
                {
                    Success = false,
                    ErrorMessage = ex.Message,
                    UserFriendlyMessage = TranslateDomainException(ex),
                    ValidationErrors = ExtractValidationErrors(ex)
                };
            }
            catch (Exception ex)
            {
                return new SalesOrderCreationResult
                {
                    Success = false,
                    ErrorMessage = ex.Message,
                    UserFriendlyMessage = "An unexpected error occurred while creating the sales order"
                };
            }
        }

        public async Task<SalesOrderCancellationResult> CancelSalesOrderAsync(CancelSalesOrderUiRequest request, CancellationToken cancellationToken = default)
        {
            try
            {
                // Validate UI request
                var validationErrors = ValidateCancelSalesOrderRequest(request);
                if (validationErrors.Any())
                {
                    return new SalesOrderCancellationResult
                    {
                        Success = false,
                        ValidationErrors = validationErrors,
                        UserFriendlyMessage = "Please correct the validation errors"
                    };
                }

                // Map UI request to application command
                var command = new CancelSalesOrderCommand
                {
                    OrderId = request.OrderId,
                    Reason = request.Reason,
                    CancelledByUserId = request.CancelledByUserId
                };

                // Call application service
                await _cancelSalesOrderUseCase.ExecuteAsync(command, cancellationToken);

                return new SalesOrderCancellationResult
                {
                    Success = true,
                    UserFriendlyMessage = "Sales order cancelled successfully"
                };
            }
            catch (DomainException ex)
            {
                return new SalesOrderCancellationResult
                {
                    Success = false,
                    ErrorMessage = ex.Message,
                    UserFriendlyMessage = TranslateDomainException(ex),
                    ValidationErrors = ExtractValidationErrors(ex)
                };
            }
            catch (Exception ex)
            {
                return new SalesOrderCancellationResult
                {
                    Success = false,
                    ErrorMessage = ex.Message,
                    UserFriendlyMessage = "An unexpected error occurred while cancelling the sales order"
                };
            }
        }

        public async Task<StockReservationResult> ReserveStockAsync(ReserveStockUiRequest request, CancellationToken cancellationToken = default)
        {
            try
            {
                // Validate UI request
                var validationErrors = ValidateReserveStockRequest(request);
                if (validationErrors.Any())
                {
                    return new StockReservationResult
                    {
                        Success = false,
                        ValidationErrors = validationErrors,
                        UserFriendlyMessage = "Please correct the validation errors"
                    };
                }

                // Map UI request to application command
                var command = new ReserveStockForOrderCommand
                {
                    OrderId = request.OrderId,
                    Reservations = request.Reservations.Select(r => new StockReservationCommand
                    {
                        ProductId = r.ProductId,
                        Quantity = r.Quantity
                    }).ToList(),
                    ExpiresAt = request.ExpiresAt
                };

                // Call application service
                var reservationIds = await _reserveStockForOrderUseCase.ExecuteAsync(command, cancellationToken);

                return new StockReservationResult
                {
                    Success = true,
                    ReservationIds = reservationIds,
                    UserFriendlyMessage = "Stock reserved successfully"
                };
            }
            catch (DomainException ex)
            {
                return new StockReservationResult
                {
                    Success = false,
                    ErrorMessage = ex.Message,
                    UserFriendlyMessage = TranslateDomainException(ex),
                    ValidationErrors = ExtractValidationErrors(ex)
                };
            }
            catch (Exception ex)
            {
                return new StockReservationResult
                {
                    Success = false,
                    ErrorMessage = ex.Message,
                    UserFriendlyMessage = "An unexpected error occurred while reserving stock"
                };
            }
        }

        public async Task<CreditCheckResult> CheckCreditLimitAsync(CheckCreditUiRequest request, CancellationToken cancellationToken = default)
        {
            try
            {
                // Validate UI request
                var validationErrors = ValidateCheckCreditRequest(request);
                if (validationErrors.Any())
                {
                    return new CreditCheckResult
                    {
                        Success = false,
                        ValidationErrors = validationErrors,
                        UserFriendlyMessage = "Please correct the validation errors"
                    };
                }

                // Map UI request to application command
                var command = new CheckCreditLimitCommand
                {
                    CustomerId = request.CustomerId,
                    OrderAmount = request.OrderAmount
                };

                // Call application service
                await _checkCreditLimitUseCase.ExecuteAsync(command, cancellationToken);

                return new CreditCheckResult
                {
                    Success = true,
                    UserFriendlyMessage = "Credit limit check passed"
                };
            }
            catch (CreditLimitExceededException ex)
            {
                return new CreditCheckResult
                {
                    Success = false,
                    ErrorMessage = ex.Message,
                    UserFriendlyMessage = "Order exceeds customer credit limit"
                };
            }
            catch (DomainException ex)
            {
                return new CreditCheckResult
                {
                    Success = false,
                    ErrorMessage = ex.Message,
                    UserFriendlyMessage = TranslateDomainException(ex),
                    ValidationErrors = ExtractValidationErrors(ex)
                };
            }
            catch (Exception ex)
            {
                return new CreditCheckResult
                {
                    Success = false,
                    ErrorMessage = ex.Message,
                    UserFriendlyMessage = "An unexpected error occurred while checking credit limit"
                };
            }
        }

        private static List<SalesOrderValidationError> ValidateCreateSalesOrderRequest(CreateSalesOrderUiRequest request)
        {
            var errors = new List<SalesOrderValidationError>();

            if (request.CustomerId <= 0)
                errors.Add(new SalesOrderValidationError { PropertyName = nameof(request.CustomerId), ErrorMessage = "Customer is required" });

            if (request.Lines == null || !request.Lines.Any())
                errors.Add(new SalesOrderValidationError { PropertyName = nameof(request.Lines), ErrorMessage = "Order must have at least one line" });

            if (request.Lines != null)
            {
                for (int i = 0; i < request.Lines.Count; i++)
                {
                    var line = request.Lines[i];
                    if (line.ProductId <= 0)
                        errors.Add(new SalesOrderValidationError { PropertyName = $"Lines[{i}].ProductId", ErrorMessage = "Product is required" });

                    if (line.Quantity <= 0)
                        errors.Add(new SalesOrderValidationError { PropertyName = $"Lines[{i}].Quantity", ErrorMessage = "Quantity must be positive" });

                    if (line.UnitPrice < 0)
                        errors.Add(new SalesOrderValidationError { PropertyName = $"Lines[{i}].UnitPrice", ErrorMessage = "Unit price cannot be negative" });

                    if (line.Discount < 0 || line.Discount > 100)
                        errors.Add(new SalesOrderValidationError { PropertyName = $"Lines[{i}].Discount", ErrorMessage = "Discount must be between 0 and 100" });
                }
            }

            return errors;
        }

        private static List<SalesOrderValidationError> ValidateCancelSalesOrderRequest(CancelSalesOrderUiRequest request)
        {
            var errors = new List<SalesOrderValidationError>();

            if (request.OrderId <= 0)
                errors.Add(new SalesOrderValidationError { PropertyName = nameof(request.OrderId), ErrorMessage = "Valid order ID is required" });

            if (request.CancelledByUserId <= 0)
                errors.Add(new SalesOrderValidationError { PropertyName = nameof(request.CancelledByUserId), ErrorMessage = "Valid user ID is required" });

            return errors;
        }

        private static List<SalesOrderValidationError> ValidateReserveStockRequest(ReserveStockUiRequest request)
        {
            var errors = new List<SalesOrderValidationError>();

            if (request.OrderId <= 0)
                errors.Add(new SalesOrderValidationError { PropertyName = nameof(request.OrderId), ErrorMessage = "Valid order ID is required" });

            if (request.Reservations == null || !request.Reservations.Any())
                errors.Add(new SalesOrderValidationError { PropertyName = nameof(request.Reservations), ErrorMessage = "At least one reservation is required" });

            if (request.Reservations != null)
            {
                for (int i = 0; i < request.Reservations.Count; i++)
                {
                    var reservation = request.Reservations[i];
                    if (reservation.ProductId <= 0)
                        errors.Add(new SalesOrderValidationError { PropertyName = $"Reservations[{i}].ProductId", ErrorMessage = "Product is required" });

                    if (reservation.Quantity <= 0)
                        errors.Add(new SalesOrderValidationError { PropertyName = $"Reservations[{i}].Quantity", ErrorMessage = "Quantity must be positive" });
                }
            }

            return errors;
        }

        private static List<SalesOrderValidationError> ValidateCheckCreditRequest(CheckCreditUiRequest request)
        {
            var errors = new List<SalesOrderValidationError>();

            if (request.CustomerId <= 0)
                errors.Add(new SalesOrderValidationError { PropertyName = nameof(request.CustomerId), ErrorMessage = "Valid customer ID is required" });

            if (request.OrderAmount < 0)
                errors.Add(new SalesOrderValidationError { PropertyName = nameof(request.OrderAmount), ErrorMessage = "Order amount cannot be negative" });

            return errors;
        }

        private static List<SalesOrderValidationError> ExtractValidationErrors(DomainException ex)
        {
            var errors = new List<SalesOrderValidationError>();

            if (ex.Message.Contains("Customer"))
                errors.Add(new SalesOrderValidationError { PropertyName = "CustomerId", ErrorMessage = "Invalid customer" });

            if (ex.Message.Contains("Product"))
                errors.Add(new SalesOrderValidationError { PropertyName = "ProductId", ErrorMessage = "Invalid product" });

            if (ex.Message.Contains("Quantity"))
                errors.Add(new SalesOrderValidationError { PropertyName = "Quantity", ErrorMessage = "Invalid quantity" });

            if (ex.Message.Contains("Price"))
                errors.Add(new SalesOrderValidationError { PropertyName = "UnitPrice", ErrorMessage = "Invalid price" });

            if (ex.Message.Contains("Discount"))
                errors.Add(new SalesOrderValidationError { PropertyName = "Discount", ErrorMessage = "Invalid discount" });

            return errors;
        }

        private static string TranslateDomainException(DomainException ex)
        {
            return ex.Message switch
            {
                var msg when msg.Contains("Customer") => "Please select a valid customer",
                var msg when msg.Contains("Product") => "Please select valid products",
                var msg when msg.Contains("Quantity") => "Please enter valid quantities",
                var msg when msg.Contains("Price") => "Please enter valid prices",
                var msg when msg.Contains("Discount") => "Please enter valid discounts",
                _ => "Please check the information entered and try again"
            };
        }
    }
}
