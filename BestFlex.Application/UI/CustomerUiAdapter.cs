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
    // Customer UI Adapter - Safe bridge between UI and Application Services
    public interface ICustomerUiAdapter
    {
        Task<CustomerCreationResult> CreateCustomerAsync(CreateCustomerUiRequest request, CancellationToken cancellationToken = default);
        Task<CustomerUpdateResult> UpdateCustomerAsync(UpdateCustomerUiRequest request, CancellationToken cancellationToken = default);
        Task<CreditLimitChangeResult> ChangeCreditLimitAsync(ChangeCreditLimitUiRequest request, CancellationToken cancellationToken = default);
        Task<CustomerDeactivationResult> DeactivateCustomerAsync(DeactivateCustomerUiRequest request, CancellationToken cancellationToken = default);
    }

    // UI Request DTOs
    public class CreateCustomerUiRequest
    {
        public string Name { get; set; } = string.Empty;
        public string TaxId { get; set; } = string.Empty;
        public decimal CreditLimit { get; set; }
        public int PaymentTermsDays { get; set; } = 30;
    }

    public class UpdateCustomerUiRequest
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public decimal CreditLimit { get; set; }
        public int PaymentTermsDays { get; set; }
    }

    public class ChangeCreditLimitUiRequest
    {
        public int CustomerId { get; set; }
        public decimal NewCreditLimit { get; set; }
        public string Reason { get; set; } = string.Empty;
    }

    public class DeactivateCustomerUiRequest
    {
        public int CustomerId { get; set; }
        public string Reason { get; set; } = string.Empty;
    }

    // UI Result DTOs
    public class CustomerCreationResult
    {
        public bool Success { get; set; }
        public int CustomerId { get; set; }
        public string ErrorMessage { get; set; } = string.Empty;
        public string UserFriendlyMessage { get; set; } = string.Empty;
        public List<CustomerValidationError> ValidationErrors { get; set; } = new();
    }

    public class CustomerUpdateResult
    {
        public bool Success { get; set; }
        public string ErrorMessage { get; set; } = string.Empty;
        public string UserFriendlyMessage { get; set; } = string.Empty;
        public List<CustomerValidationError> ValidationErrors { get; set; } = new();
    }

    public class CreditLimitChangeResult
    {
        public bool Success { get; set; }
        public string ErrorMessage { get; set; } = string.Empty;
        public string UserFriendlyMessage { get; set; } = string.Empty;
        public List<CustomerValidationError> ValidationErrors { get; set; } = new();
    }

    public class CustomerDeactivationResult
    {
        public bool Success { get; set; }
        public string ErrorMessage { get; set; } = string.Empty;
        public string UserFriendlyMessage { get; set; } = string.Empty;
        public List<CustomerValidationError> ValidationErrors { get; set; } = new();
    }

    public class CustomerValidationError
    {
        public string PropertyName { get; set; } = string.Empty;
        public string ErrorMessage { get; set; } = string.Empty;
    }

    // Implementation
    public class CustomerUiAdapter : ICustomerUiAdapter
    {
        private readonly ICreateCustomerUseCase _createCustomerUseCase;
        private readonly IUpdateCustomerUseCase _updateCustomerUseCase;
        private readonly IChangeCreditLimitUseCase _changeCreditLimitUseCase;
        private readonly IDeactivateCustomerUseCase _deactivateCustomerUseCase;

        public CustomerUiAdapter(
            ICreateCustomerUseCase createCustomerUseCase,
            IUpdateCustomerUseCase updateCustomerUseCase,
            IChangeCreditLimitUseCase changeCreditLimitUseCase,
            IDeactivateCustomerUseCase deactivateCustomerUseCase)
        {
            _createCustomerUseCase = createCustomerUseCase ?? throw new ArgumentNullException(nameof(createCustomerUseCase));
            _updateCustomerUseCase = updateCustomerUseCase ?? throw new ArgumentNullException(nameof(updateCustomerUseCase));
            _changeCreditLimitUseCase = changeCreditLimitUseCase ?? throw new ArgumentNullException(nameof(changeCreditLimitUseCase));
            _deactivateCustomerUseCase = deactivateCustomerUseCase ?? throw new ArgumentNullException(nameof(deactivateCustomerUseCase));
        }

        public async Task<CustomerCreationResult> CreateCustomerAsync(CreateCustomerUiRequest request, CancellationToken cancellationToken = default)
        {
            try
            {
                // Validate UI request
                var validationErrors = ValidateCreateCustomerRequest(request);
                if (validationErrors.Any())
                {
                    return new CustomerCreationResult
                    {
                        Success = false,
                        ValidationErrors = validationErrors,
                        UserFriendlyMessage = "Please correct the validation errors"
                    };
                }

                // Map UI request to application command
                var command = new CreateCustomerCommand
                {
                    Name = request.Name,
                    TaxId = request.TaxId,
                    CreditLimit = request.CreditLimit,
                    PaymentTermsDays = request.PaymentTermsDays
                };

                // Call application service
                var customerId = await _createCustomerUseCase.ExecuteAsync(command, cancellationToken);

                return new CustomerCreationResult
                {
                    Success = true,
                    CustomerId = customerId,
                    UserFriendlyMessage = "Customer created successfully"
                };
            }
            catch (DomainException ex)
            {
                return new CustomerCreationResult
                {
                    Success = false,
                    ErrorMessage = ex.Message,
                    UserFriendlyMessage = TranslateDomainException(ex),
                    ValidationErrors = ExtractValidationErrors(ex)
                };
            }
            catch (Exception ex)
            {
                return new CustomerCreationResult
                {
                    Success = false,
                    ErrorMessage = ex.Message,
                    UserFriendlyMessage = "An unexpected error occurred while creating the customer"
                };
            }
        }

        public async Task<CustomerUpdateResult> UpdateCustomerAsync(UpdateCustomerUiRequest request, CancellationToken cancellationToken = default)
        {
            try
            {
                // Validate UI request
                var validationErrors = ValidateUpdateCustomerRequest(request);
                if (validationErrors.Any())
                {
                    return new CustomerUpdateResult
                    {
                        Success = false,
                        ValidationErrors = validationErrors,
                        UserFriendlyMessage = "Please correct the validation errors"
                    };
                }

                // Map UI request to application command
                var command = new UpdateCustomerCommand
                {
                    Id = request.Id,
                    Name = request.Name,
                    CreditLimit = request.CreditLimit,
                    PaymentTermsDays = request.PaymentTermsDays
                };

                // Call application service
                await _updateCustomerUseCase.ExecuteAsync(command, cancellationToken);

                return new CustomerUpdateResult
                {
                    Success = true,
                    UserFriendlyMessage = "Customer updated successfully"
                };
            }
            catch (DomainException ex)
            {
                return new CustomerUpdateResult
                {
                    Success = false,
                    ErrorMessage = ex.Message,
                    UserFriendlyMessage = TranslateDomainException(ex),
                    ValidationErrors = ExtractValidationErrors(ex)
                };
            }
            catch (Exception ex)
            {
                return new CustomerUpdateResult
                {
                    Success = false,
                    ErrorMessage = ex.Message,
                    UserFriendlyMessage = "An unexpected error occurred while updating the customer"
                };
            }
        }

        public async Task<CreditLimitChangeResult> ChangeCreditLimitAsync(ChangeCreditLimitUiRequest request, CancellationToken cancellationToken = default)
        {
            try
            {
                // Validate UI request
                var validationErrors = ValidateChangeCreditLimitRequest(request);
                if (validationErrors.Any())
                {
                    return new CreditLimitChangeResult
                    {
                        Success = false,
                        ValidationErrors = validationErrors,
                        UserFriendlyMessage = "Please correct the validation errors"
                    };
                }

                // Map UI request to application command
                var command = new ChangeCreditLimitCommand
                {
                    CustomerId = request.CustomerId,
                    NewCreditLimit = request.NewCreditLimit,
                    Reason = request.Reason
                };

                // Call application service
                await _changeCreditLimitUseCase.ExecuteAsync(command, cancellationToken);

                return new CreditLimitChangeResult
                {
                    Success = true,
                    UserFriendlyMessage = "Credit limit changed successfully"
                };
            }
            catch (DomainException ex)
            {
                return new CreditLimitChangeResult
                {
                    Success = false,
                    ErrorMessage = ex.Message,
                    UserFriendlyMessage = TranslateDomainException(ex),
                    ValidationErrors = ExtractValidationErrors(ex)
                };
            }
            catch (Exception ex)
            {
                return new CreditLimitChangeResult
                {
                    Success = false,
                    ErrorMessage = ex.Message,
                    UserFriendlyMessage = "An unexpected error occurred while changing the credit limit"
                };
            }
        }

        public async Task<CustomerDeactivationResult> DeactivateCustomerAsync(DeactivateCustomerUiRequest request, CancellationToken cancellationToken = default)
        {
            try
            {
                // Validate UI request
                var validationErrors = ValidateDeactivateCustomerRequest(request);
                if (validationErrors.Any())
                {
                    return new CustomerDeactivationResult
                    {
                        Success = false,
                        ValidationErrors = validationErrors,
                        UserFriendlyMessage = "Please correct the validation errors"
                    };
                }

                // Map UI request to application command
                var command = new DeactivateCustomerCommand
                {
                    CustomerId = request.CustomerId,
                    Reason = request.Reason
                };

                // Call application service
                await _deactivateCustomerUseCase.ExecuteAsync(command, cancellationToken);

                return new CustomerDeactivationResult
                {
                    Success = true,
                    UserFriendlyMessage = "Customer deactivated successfully"
                };
            }
            catch (DomainException ex)
            {
                return new CustomerDeactivationResult
                {
                    Success = false,
                    ErrorMessage = ex.Message,
                    UserFriendlyMessage = TranslateDomainException(ex),
                    ValidationErrors = ExtractValidationErrors(ex)
                };
            }
            catch (Exception ex)
            {
                return new CustomerDeactivationResult
                {
                    Success = false,
                    ErrorMessage = ex.Message,
                    UserFriendlyMessage = "An unexpected error occurred while deactivating the customer"
                };
            }
        }

        private static List<CustomerValidationError> ValidateCreateCustomerRequest(CreateCustomerUiRequest request)
        {
            var errors = new List<CustomerValidationError>();

            if (string.IsNullOrWhiteSpace(request.Name))
                errors.Add(new CustomerValidationError { PropertyName = nameof(request.Name), ErrorMessage = "Customer name is required" });

            if (string.IsNullOrWhiteSpace(request.TaxId))
                errors.Add(new CustomerValidationError { PropertyName = nameof(request.TaxId), ErrorMessage = "Tax ID is required" });

            if (request.CreditLimit < 0)
                errors.Add(new CustomerValidationError { PropertyName = nameof(request.CreditLimit), ErrorMessage = "Credit limit cannot be negative" });

            if (request.PaymentTermsDays < 0)
                errors.Add(new CustomerValidationError { PropertyName = nameof(request.PaymentTermsDays), ErrorMessage = "Payment terms days cannot be negative" });

            return errors;
        }

        private static List<CustomerValidationError> ValidateUpdateCustomerRequest(UpdateCustomerUiRequest request)
        {
            var errors = new List<CustomerValidationError>();

            if (request.Id <= 0)
                errors.Add(new CustomerValidationError { PropertyName = nameof(request.Id), ErrorMessage = "Valid customer ID is required" });

            if (string.IsNullOrWhiteSpace(request.Name))
                errors.Add(new CustomerValidationError { PropertyName = nameof(request.Name), ErrorMessage = "Customer name is required" });

            if (request.CreditLimit < 0)
                errors.Add(new CustomerValidationError { PropertyName = nameof(request.CreditLimit), ErrorMessage = "Credit limit cannot be negative" });

            if (request.PaymentTermsDays < 0)
                errors.Add(new CustomerValidationError { PropertyName = nameof(request.PaymentTermsDays), ErrorMessage = "Payment terms days cannot be negative" });

            return errors;
        }

        private static List<CustomerValidationError> ValidateChangeCreditLimitRequest(ChangeCreditLimitUiRequest request)
        {
            var errors = new List<CustomerValidationError>();

            if (request.CustomerId <= 0)
                errors.Add(new CustomerValidationError { PropertyName = nameof(request.CustomerId), ErrorMessage = "Valid customer ID is required" });

            if (request.NewCreditLimit < 0)
                errors.Add(new CustomerValidationError { PropertyName = nameof(request.NewCreditLimit), ErrorMessage = "New credit limit cannot be negative" });

            return errors;
        }

        private static List<CustomerValidationError> ValidateDeactivateCustomerRequest(DeactivateCustomerUiRequest request)
        {
            var errors = new List<CustomerValidationError>();

            if (request.CustomerId <= 0)
                errors.Add(new CustomerValidationError { PropertyName = nameof(request.CustomerId), ErrorMessage = "Valid customer ID is required" });

            return errors;
        }

        private static List<CustomerValidationError> ExtractValidationErrors(DomainException ex)
        {
            var errors = new List<CustomerValidationError>();

            if (ex.Message.Contains("Name"))
                errors.Add(new CustomerValidationError { PropertyName = "Name", ErrorMessage = "Invalid customer name" });

            if (ex.Message.Contains("TaxId"))
                errors.Add(new CustomerValidationError { PropertyName = "TaxId", ErrorMessage = "Invalid tax ID" });

            if (ex.Message.Contains("CreditLimit") || ex.Message.Contains("credit limit"))
                errors.Add(new CustomerValidationError { PropertyName = "CreditLimit", ErrorMessage = "Invalid credit limit" });

            return errors;
        }

        private static string TranslateDomainException(DomainException ex)
        {
            return ex.Message switch
            {
                var msg when msg.Contains("credit limit") => "Please enter a valid credit limit",
                var msg when msg.Contains("Name") => "Please enter a valid customer name",
                var msg when msg.Contains("TaxId") => "Please enter a valid tax ID",
                _ => "Please check the information entered and try again"
            };
        }
    }

    // Lookup DTOs
    public class CustomerLookupDto
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public string Phone { get; set; } = string.Empty;
        public string Address { get; set; } = string.Empty;
        public decimal CreditLimit { get; set; }
        public decimal CurrentBalance { get; set; }
        public bool IsActive { get; set; }
        public DateTime CreatedDate { get; set; }
    }
}
