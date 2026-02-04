using System;
using System.Threading.Tasks;
using BestFlex.Application.UI;
using Microsoft.Extensions.DependencyInjection;

namespace BestFlex.Shell.Tests
{
    public class CustomerManagementTest
    {
        public static async Task TestCustomerManagementAsync()
        {
            // Setup DI container
            var services = new ServiceCollection();
            
            // Register application services
            services.AddSingleton<BestFlex.Application.Abstractions.IUnitOfWork, BestFlex.Persistence.UnitOfWork>();
            services.AddSingleton<BestFlex.Domain.IForensicLogger, BestFlex.Infrastructure.Diagnostics.ForensicLogger>();
            
            // Register use cases
            services.AddSingleton<BestFlex.Application.UseCases.ICreateCustomerUseCase, BestFlex.Application.UseCases.CreateCustomerUseCase>();
            services.AddSingleton<BestFlex.Application.UseCases.IUpdateCustomerUseCase, BestFlex.Application.UseCases.UpdateCustomerUseCase>();
            services.AddSingleton<BestFlex.Application.UseCases.IChangeCreditLimitUseCase, BestFlex.Application.UseCases.ChangeCreditLimitUseCase>();
            services.AddSingleton<BestFlex.Application.UseCases.IDeactivateCustomerUseCase, BestFlex.Application.UseCases.DeactivateCustomerUseCase>();
            
            // Register UI adapters
            services.AddSingleton<BestFlex.Application.UI.ICustomerUiAdapter, BestFlex.Application.UI.CustomerUiAdapter>();
            
            var serviceProvider = services.BuildServiceProvider();
            var customerAdapter = serviceProvider.GetRequiredService<ICustomerUiAdapter>();

            Console.WriteLine("=== Customer Management Test ===");

            // Test 1: Create Customer
            Console.WriteLine("\n1. Creating Customer...");
            var createRequest = new CreateCustomerUiRequest
            {
                Name = "Test Customer Corp",
                TaxId = "123456789",
                CreditLimit = 15000,
                PaymentTermsDays = 30
            };

            var createResult = await customerAdapter.CreateCustomerAsync(createRequest);
            Console.WriteLine($"   Success: {createResult.Success}");
            Console.WriteLine($"   Customer ID: {createResult.CustomerId}");
            Console.WriteLine($"   Message: {createResult.UserFriendlyMessage}");
            if (!createResult.Success)
            {
                Console.WriteLine($"   Errors: {string.Join(", ", createResult.ValidationErrors.Select(e => e.ErrorMessage))}");
            }

            // Test 2: Update Customer
            Console.WriteLine("\n2. Updating Customer...");
            var updateRequest = new UpdateCustomerUiRequest
            {
                Id = createResult.CustomerId,
                Name = "Updated Test Customer Corp",
                CreditLimit = 20000,
                PaymentTermsDays = 45
            };

            var updateResult = await customerAdapter.UpdateCustomerAsync(updateRequest);
            Console.WriteLine($"   Success: {updateResult.Success}");
            Console.WriteLine($"   Message: {updateResult.UserFriendlyMessage}");
            if (!updateResult.Success)
            {
                Console.WriteLine($"   Errors: {string.Join(", ", updateResult.ValidationErrors.Select(e => e.ErrorMessage))}");
            }

            // Test 3: Change Credit Limit
            Console.WriteLine("\n3. Changing Credit Limit...");
            var creditLimitRequest = new ChangeCreditLimitUiRequest
            {
                CustomerId = createResult.CustomerId,
                NewCreditLimit = 25000,
                Reason = "Good payment history"
            };

            var creditLimitResult = await customerAdapter.ChangeCreditLimitAsync(creditLimitRequest);
            Console.WriteLine($"   Success: {creditLimitResult.Success}");
            Console.WriteLine($"   Message: {creditLimitResult.UserFriendlyMessage}");
            if (!creditLimitResult.Success)
            {
                Console.WriteLine($"   Errors: {string.Join(", ", creditLimitResult.ValidationErrors.Select(e => e.ErrorMessage))}");
            }

            // Test 4: Deactivate Customer
            Console.WriteLine("\n4. Deactivating Customer...");
            var deactivateRequest = new DeactivateCustomerUiRequest
            {
                CustomerId = createResult.CustomerId,
                Reason = "Business closure"
            };

            var deactivateResult = await customerAdapter.DeactivateCustomerAsync(deactivateRequest);
            Console.WriteLine($"   Success: {deactivateResult.Success}");
            Console.WriteLine($"   Message: {deactivateResult.UserFriendlyMessage}");
            if (!deactivateResult.Success)
            {
                Console.WriteLine($"   Errors: {string.Join(", ", deactivateResult.ValidationErrors.Select(e => e.ErrorMessage))}");
            }

            // Test 5: Validation Errors
            Console.WriteLine("\n5. Testing Validation (Invalid Credit Limit)...");
            var invalidRequest = new CreateCustomerUiRequest
            {
                Name = "Invalid Customer",
                TaxId = "987654321",
                CreditLimit = -1000, // Invalid negative credit limit
                PaymentTermsDays = 30
            };

            var invalidResult = await customerAdapter.CreateCustomerAsync(invalidRequest);
            Console.WriteLine($"   Success: {invalidResult.Success}");
            Console.WriteLine($"   Message: {invalidResult.UserFriendlyMessage}");
            if (!invalidResult.Success)
            {
                Console.WriteLine($"   Validation Errors: {string.Join(", ", invalidResult.ValidationErrors.Select(e => e.ErrorMessage))}");
            }

            Console.WriteLine("\n=== Test Complete ===");
        }
    }
}
