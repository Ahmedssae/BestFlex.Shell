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
    // Product UI Adapter - Safe bridge between UI and Application Services
    public interface IProductUiAdapter
    {
        Task<ProductListResult> GetProductsAsync(CancellationToken cancellationToken = default);
        Task<ProductCreationResult> CreateProductAsync(CreateProductUiRequest request, CancellationToken cancellationToken = default);
        Task<ProductUpdateResult> UpdateProductAsync(UpdateProductUiRequest request, CancellationToken cancellationToken = default);
        Task<PriceTierAdditionResult> AddPriceTierAsync(AddPriceTierUiRequest request, CancellationToken cancellationToken = default);
        Task<ProductDeactivationResult> DeactivateProductAsync(DeactivateProductUiRequest request, CancellationToken cancellationToken = default);
    }

    // UI Request DTOs
    public class CreateProductUiRequest
    {
        public string SKU { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public decimal Cost { get; set; }
        public decimal Price { get; set; }
    }

    public class UpdateProductUiRequest
    {
        public int Id { get; set; }
        public string Description { get; set; } = string.Empty;
        public decimal Cost { get; set; }
        public decimal Price { get; set; }
    }

    public class AddPriceTierUiRequest
    {
        public int ProductId { get; set; }
        public decimal QuantityFrom { get; set; }
        public decimal QuantityTo { get; set; }
        public decimal Price { get; set; }
        public string Currency { get; set; } = "USD";
    }

    public class DeactivateProductUiRequest
    {
        public int ProductId { get; set; }
        public string Reason { get; set; } = string.Empty;
    }

    // UI Result DTOs
    public class ProductListResult
    {
        public bool Success { get; set; }
        public List<ProductUiDto> Products { get; set; } = new();
        public string ErrorMessage { get; set; } = string.Empty;
        public string UserFriendlyMessage { get; set; } = string.Empty;
        public List<ProductValidationError> ValidationErrors { get; set; } = new();
    }

    public class ProductUiDto
    {
        public int Id { get; set; }
        public string SKU { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public decimal Cost { get; set; }
        public decimal BasePrice { get; set; }
        public bool IsActive { get; set; }
        public DateTime CreatedAt { get; set; }
        public List<PriceTierDto> PriceTiers { get; set; } = new();
    }

    public class PriceTierDto
    {
        public int Id { get; set; }
        public decimal QuantityFrom { get; set; }
        public decimal QuantityTo { get; set; }
        public decimal Price { get; set; }
        public string Currency { get; set; } = "USD";
    }

    public class ProductCreationResult
    {
        public bool Success { get; set; }
        public int ProductId { get; set; }
        public string ErrorMessage { get; set; } = string.Empty;
        public string UserFriendlyMessage { get; set; } = string.Empty;
        public List<ProductValidationError> ValidationErrors { get; set; } = new();
    }

    public class ProductUpdateResult
    {
        public bool Success { get; set; }
        public string ErrorMessage { get; set; } = string.Empty;
        public string UserFriendlyMessage { get; set; } = string.Empty;
        public List<ProductValidationError> ValidationErrors { get; set; } = new();
    }

    public class PriceTierAdditionResult
    {
        public bool Success { get; set; }
        public string ErrorMessage { get; set; } = string.Empty;
        public string UserFriendlyMessage { get; set; } = string.Empty;
        public List<ProductValidationError> ValidationErrors { get; set; } = new();
    }

    public class ProductDeactivationResult
    {
        public bool Success { get; set; }
        public string ErrorMessage { get; set; } = string.Empty;
        public string UserFriendlyMessage { get; set; } = string.Empty;
        public List<ProductValidationError> ValidationErrors { get; set; } = new();
    }

    public class ProductValidationError
    {
        public string PropertyName { get; set; } = string.Empty;
        public string ErrorMessage { get; set; } = string.Empty;
    }

    // Implementation
    public class ProductUiAdapter : IProductUiAdapter
    {
        private readonly ICreateProductUseCase _createProductUseCase;
        private readonly IUpdateProductUseCase _updateProductUseCase;
        private readonly IAddPriceTierUseCase _addPriceTierUseCase;
        private readonly IDeactivateProductUseCase _deactivateProductUseCase;

        public ProductUiAdapter(
            ICreateProductUseCase createProductUseCase,
            IUpdateProductUseCase updateProductUseCase,
            IAddPriceTierUseCase addPriceTierUseCase,
            IDeactivateProductUseCase deactivateProductUseCase)
        {
            _createProductUseCase = createProductUseCase ?? throw new ArgumentNullException(nameof(createProductUseCase));
            _updateProductUseCase = updateProductUseCase ?? throw new ArgumentNullException(nameof(updateProductUseCase));
            _addPriceTierUseCase = addPriceTierUseCase ?? throw new ArgumentNullException(nameof(addPriceTierUseCase));
            _deactivateProductUseCase = deactivateProductUseCase ?? throw new ArgumentNullException(nameof(deactivateProductUseCase));
        }

        public async Task<ProductListResult> GetProductsAsync(CancellationToken cancellationToken = default)
        {
            try
            {
                // TODO: Implement actual product retrieval from database
                // For now, return mock data to demonstrate UI functionality
                var products = new List<ProductUiDto>
                {
                    new ProductUiDto
                    {
                        Id = 1,
                        SKU = "PROD-001",
                        Name = "Sample Product 1",
                        Description = "First sample product",
                        Cost = 50.00m,
                        BasePrice = 75.00m,
                        IsActive = true,
                        CreatedAt = DateTime.UtcNow.AddDays(-30),
                        PriceTiers = new List<PriceTierDto>
                        {
                            new PriceTierDto { Id = 1, QuantityFrom = 1, QuantityTo = 9, Price = 75.00m },
                            new PriceTierDto { Id = 2, QuantityFrom = 10, QuantityTo = 99, Price = 70.00m },
                            new PriceTierDto { Id = 3, QuantityFrom = 100, QuantityTo = 999, Price = 65.00m }
                        }
                    },
                    new ProductUiDto
                    {
                        Id = 2,
                        SKU = "PROD-002",
                        Name = "Sample Product 2",
                        Description = "Second sample product",
                        Cost = 25.00m,
                        BasePrice = 40.00m,
                        IsActive = true,
                        CreatedAt = DateTime.UtcNow.AddDays(-15),
                        PriceTiers = new List<PriceTierDto>
                        {
                            new PriceTierDto { Id = 4, QuantityFrom = 1, QuantityTo = 49, Price = 40.00m },
                            new PriceTierDto { Id = 5, QuantityFrom = 50, QuantityTo = 999, Price = 35.00m }
                        }
                    },
                    new ProductUiDto
                    {
                        Id = 3,
                        SKU = "PROD-003",
                        Name = "Inactive Product",
                        Description = "Inactive sample product",
                        Cost = 30.00m,
                        BasePrice = 45.00m,
                        IsActive = false,
                        CreatedAt = DateTime.UtcNow.AddDays(-60),
                        PriceTiers = new List<PriceTierDto>()
                    }
                };

                return new ProductListResult
                {
                    Success = true,
                    Products = products,
                    UserFriendlyMessage = "Products loaded successfully"
                };
            }
            catch (Exception ex)
            {
                return new ProductListResult
                {
                    Success = false,
                    Products = new List<ProductUiDto>(),
                    ErrorMessage = ex.Message,
                    UserFriendlyMessage = "Failed to load products"
                };
            }
        }

        public async Task<ProductCreationResult> CreateProductAsync(CreateProductUiRequest request, CancellationToken cancellationToken = default)
        {
            try
            {
                // Validate UI request
                var validationErrors = ValidateCreateProductRequest(request);
                if (validationErrors.Any())
                {
                    return new ProductCreationResult
                    {
                        Success = false,
                        ValidationErrors = validationErrors,
                        UserFriendlyMessage = "Please correct the validation errors"
                    };
                }

                // Map UI request to application command
                var command = new CreateProductCommand
                {
                    SKU = request.SKU,
                    Description = request.Description,
                    Cost = request.Cost,
                    Price = request.Price
                };

                // Call application service
                var productId = await _createProductUseCase.ExecuteAsync(command, cancellationToken);

                return new ProductCreationResult
                {
                    Success = true,
                    ProductId = productId,
                    UserFriendlyMessage = "Product created successfully"
                };
            }
            catch (DomainException ex)
            {
                return new ProductCreationResult
                {
                    Success = false,
                    ErrorMessage = ex.Message,
                    UserFriendlyMessage = TranslateDomainException(ex),
                    ValidationErrors = ExtractValidationErrors(ex)
                };
            }
            catch (Exception ex)
            {
                return new ProductCreationResult
                {
                    Success = false,
                    ErrorMessage = ex.Message,
                    UserFriendlyMessage = "An unexpected error occurred while creating the product"
                };
            }
        }

        public async Task<ProductUpdateResult> UpdateProductAsync(UpdateProductUiRequest request, CancellationToken cancellationToken = default)
        {
            try
            {
                // Validate UI request
                var validationErrors = ValidateUpdateProductRequest(request);
                if (validationErrors.Any())
                {
                    return new ProductUpdateResult
                    {
                        Success = false,
                        ValidationErrors = validationErrors,
                        UserFriendlyMessage = "Please correct the validation errors"
                    };
                }

                // Map UI request to application command
                var command = new UpdateProductCommand
                {
                    Id = request.Id,
                    Description = request.Description,
                    Cost = request.Cost,
                    Price = request.Price
                };

                // Call application service
                await _updateProductUseCase.ExecuteAsync(command, cancellationToken);

                return new ProductUpdateResult
                {
                    Success = true,
                    UserFriendlyMessage = "Product updated successfully"
                };
            }
            catch (DomainException ex)
            {
                return new ProductUpdateResult
                {
                    Success = false,
                    ErrorMessage = ex.Message,
                    UserFriendlyMessage = TranslateDomainException(ex),
                    ValidationErrors = ExtractValidationErrors(ex)
                };
            }
            catch (Exception ex)
            {
                return new ProductUpdateResult
                {
                    Success = false,
                    ErrorMessage = ex.Message,
                    UserFriendlyMessage = "An unexpected error occurred while updating the product"
                };
            }
        }

        public async Task<PriceTierAdditionResult> AddPriceTierAsync(AddPriceTierUiRequest request, CancellationToken cancellationToken = default)
        {
            try
            {
                // Validate UI request
                var validationErrors = ValidateAddPriceTierRequest(request);
                if (validationErrors.Any())
                {
                    return new PriceTierAdditionResult
                    {
                        Success = false,
                        ValidationErrors = validationErrors,
                        UserFriendlyMessage = "Please correct the validation errors"
                    };
                }

                // Map UI request to application command
                var command = new AddPriceTierCommand
                {
                    ProductId = request.ProductId,
                    QuantityFrom = request.QuantityFrom,
                    QuantityTo = request.QuantityTo,
                    Price = request.Price,
                    Currency = request.Currency
                };

                // Call application service
                await _addPriceTierUseCase.ExecuteAsync(command, cancellationToken);

                return new PriceTierAdditionResult
                {
                    Success = true,
                    UserFriendlyMessage = "Price tier added successfully"
                };
            }
            catch (DomainException ex)
            {
                return new PriceTierAdditionResult
                {
                    Success = false,
                    ErrorMessage = ex.Message,
                    UserFriendlyMessage = TranslateDomainException(ex),
                    ValidationErrors = ExtractValidationErrors(ex)
                };
            }
            catch (Exception ex)
            {
                return new PriceTierAdditionResult
                {
                    Success = false,
                    ErrorMessage = ex.Message,
                    UserFriendlyMessage = "An unexpected error occurred while adding the price tier"
                };
            }
        }

        public async Task<ProductDeactivationResult> DeactivateProductAsync(DeactivateProductUiRequest request, CancellationToken cancellationToken = default)
        {
            try
            {
                // Validate UI request
                var validationErrors = ValidateDeactivateProductRequest(request);
                if (validationErrors.Any())
                {
                    return new ProductDeactivationResult
                    {
                        Success = false,
                        ValidationErrors = validationErrors,
                        UserFriendlyMessage = "Please correct the validation errors"
                    };
                }

                // Map UI request to application command
                var command = new DeactivateProductCommand
                {
                    ProductId = request.ProductId,
                    Reason = request.Reason
                };

                // Call application service
                await _deactivateProductUseCase.ExecuteAsync(command, cancellationToken);

                return new ProductDeactivationResult
                {
                    Success = true,
                    UserFriendlyMessage = "Product deactivated successfully"
                };
            }
            catch (DomainException ex)
            {
                return new ProductDeactivationResult
                {
                    Success = false,
                    ErrorMessage = ex.Message,
                    UserFriendlyMessage = TranslateDomainException(ex),
                    ValidationErrors = ExtractValidationErrors(ex)
                };
            }
            catch (Exception ex)
            {
                return new ProductDeactivationResult
                {
                    Success = false,
                    ErrorMessage = ex.Message,
                    UserFriendlyMessage = "An unexpected error occurred while deactivating the product"
                };
            }
        }

        private static List<ProductValidationError> ValidateCreateProductRequest(CreateProductUiRequest request)
        {
            var errors = new List<ProductValidationError>();

            if (string.IsNullOrWhiteSpace(request.SKU))
                errors.Add(new ProductValidationError { PropertyName = nameof(request.SKU), ErrorMessage = "Product SKU is required" });

            if (string.IsNullOrWhiteSpace(request.Description))
                errors.Add(new ProductValidationError { PropertyName = nameof(request.Description), ErrorMessage = "Product description is required" });

            if (request.Cost < 0)
                errors.Add(new ProductValidationError { PropertyName = nameof(request.Cost), ErrorMessage = "Product cost cannot be negative" });

            if (request.Price < 0)
                errors.Add(new ProductValidationError { PropertyName = nameof(request.Price), ErrorMessage = "Product price cannot be negative" });

            if (request.Price < request.Cost)
                errors.Add(new ProductValidationError { PropertyName = nameof(request.Price), ErrorMessage = "Selling price cannot be below cost" });

            return errors;
        }

        private static List<ProductValidationError> ValidateUpdateProductRequest(UpdateProductUiRequest request)
        {
            var errors = new List<ProductValidationError>();

            if (request.Id <= 0)
                errors.Add(new ProductValidationError { PropertyName = nameof(request.Id), ErrorMessage = "Valid product ID is required" });

            if (string.IsNullOrWhiteSpace(request.Description))
                errors.Add(new ProductValidationError { PropertyName = nameof(request.Description), ErrorMessage = "Product description is required" });

            if (request.Cost < 0)
                errors.Add(new ProductValidationError { PropertyName = nameof(request.Cost), ErrorMessage = "Product cost cannot be negative" });

            if (request.Price < 0)
                errors.Add(new ProductValidationError { PropertyName = nameof(request.Price), ErrorMessage = "Product price cannot be negative" });

            if (request.Price < request.Cost)
                errors.Add(new ProductValidationError { PropertyName = nameof(request.Price), ErrorMessage = "Selling price cannot be below cost" });

            return errors;
        }

        private static List<ProductValidationError> ValidateAddPriceTierRequest(AddPriceTierUiRequest request)
        {
            var errors = new List<ProductValidationError>();

            if (request.ProductId <= 0)
                errors.Add(new ProductValidationError { PropertyName = nameof(request.ProductId), ErrorMessage = "Valid product ID is required" });

            if (request.QuantityFrom <= 0)
                errors.Add(new ProductValidationError { PropertyName = nameof(request.QuantityFrom), ErrorMessage = "Quantity from must be positive" });

            if (request.QuantityTo <= 0)
                errors.Add(new ProductValidationError { PropertyName = nameof(request.QuantityTo), ErrorMessage = "Quantity to must be positive" });

            if (request.QuantityFrom >= request.QuantityTo)
                errors.Add(new ProductValidationError { PropertyName = nameof(request.QuantityFrom), ErrorMessage = "Quantity from must be less than quantity to" });

            if (request.Price < 0)
                errors.Add(new ProductValidationError { PropertyName = nameof(request.Price), ErrorMessage = "Price cannot be negative" });

            return errors;
        }

        private static List<ProductValidationError> ValidateDeactivateProductRequest(DeactivateProductUiRequest request)
        {
            var errors = new List<ProductValidationError>();

            if (request.ProductId <= 0)
                errors.Add(new ProductValidationError { PropertyName = nameof(request.ProductId), ErrorMessage = "Valid product ID is required" });

            return errors;
        }

        private static List<ProductValidationError> ExtractValidationErrors(DomainException ex)
        {
            var errors = new List<ProductValidationError>();

            if (ex.Message.Contains("SKU"))
                errors.Add(new ProductValidationError { PropertyName = "SKU", ErrorMessage = "Invalid product SKU" });

            if (ex.Message.Contains("Description"))
                errors.Add(new ProductValidationError { PropertyName = "Description", ErrorMessage = "Invalid product description" });

            if (ex.Message.Contains("Cost") || ex.Message.Contains("cost"))
                errors.Add(new ProductValidationError { PropertyName = "Cost", ErrorMessage = "Invalid product cost" });

            if (ex.Message.Contains("Price") || ex.Message.Contains("price"))
                errors.Add(new ProductValidationError { PropertyName = "Price", ErrorMessage = "Invalid product price" });

            if (ex.Message.Contains("QuantityFrom") || ex.Message.Contains("QuantityTo"))
                errors.Add(new ProductValidationError { PropertyName = "Quantity", ErrorMessage = "Invalid quantity range" });

            return errors;
        }

        private static string TranslateDomainException(DomainException ex)
        {
            return ex.Message switch
            {
                var msg when msg.Contains("cost") => "Please enter a valid product cost",
                var msg when msg.Contains("price") => "Please enter a valid product price",
                var msg when msg.Contains("below cost") => "Selling price cannot be below cost price",
                var msg when msg.Contains("QuantityFrom") => "Please enter a valid quantity range",
                var msg when msg.Contains("SKU") => "Please enter a valid product SKU",
                var msg when msg.Contains("Description") => "Please enter a valid product description",
                _ => "Please check the information entered and try again"
            };
        }
    }

    // Lookup DTOs
    public class ProductLookupDto
    {
        public int Id { get; set; }
        public string SKU { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public decimal Cost { get; set; }
        public decimal BasePrice { get; set; }
        public decimal AvailableStock { get; set; }
        public bool IsActive { get; set; }
        public List<PriceTierDto> PriceTiers { get; set; } = new();
        public DateTime CreatedAt { get; set; }
    }
}
