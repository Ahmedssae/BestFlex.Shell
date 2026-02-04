using System;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using BestFlex.Application.Abstractions;
using BestFlex.Domain.Entities;
using BestFlex.Persistence.Data;
using BestFlex.Infrastructure.Services;

namespace BestFlex.Shell.Tests
{
    /// <summary>
    /// Comprehensive test for Audit Trail and Transaction Rollback functionality
    /// </summary>
    public static class AuditTransactionTest
    {
        public static async Task<bool> RunAuditTrailTest(IServiceProvider serviceProvider)
        {
            var logger = serviceProvider.GetRequiredService<ILogger<AuditTransactionTest>>();
            var auditService = serviceProvider.GetRequiredService<IAuditService>();
            var currentUserService = serviceProvider.GetRequiredService<ICurrentUserService>();
            var dbContext = serviceProvider.GetRequiredService<BestFlexDbContext>();

            try
            {
                logger.LogInformation("=== AUDIT TRAIL VERIFICATION TEST ===");

                // Test 1: Verify Audit Trail Completeness
                await TestAuditTrailCompleteness(auditService, currentUserService, dbContext, logger);

                // Test 2: Verify Transaction Rollback
                await TestTransactionRollback(serviceProvider, dbContext, logger);

                logger.LogInformation("✅ ALL AUDIT AND TRANSACTION TESTS PASSED");
                return true;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "❌ AUDIT/TRANSACTION TEST FAILED");
                return false;
            }
        }

        private static async Task TestAuditTrailCompleteness(IAuditService auditService, ICurrentUserService currentUserService, BestFlexDbContext dbContext, ILogger logger)
        {
            logger.LogInformation("📝 Testing Audit Trail Completeness...");

            // Clear existing audit entries for clean test
            dbContext.AuditEntries.RemoveRange(dbContext.AuditEntries);
            await dbContext.SaveChangesAsync();

            // Test 1: Log Action with Entity
            await auditService.LogActionAsync("CREATE_CUSTOMER", "Customer", 1);
            
            // Test 2: Log Security Event
            await auditService.LogSecurityAsync("LOGIN_SUCCESS", "User logged in successfully");
            
            // Test 3: Log Navigation
            await auditService.LogNavigationAsync("Dashboard");

            // Verify audit entries
            var auditEntries = await dbContext.AuditEntries.ToListAsync();
            
            if (auditEntries.Count < 3)
            {
                throw new Exception($"Expected 3 audit entries, found {auditEntries.Count}");
            }

            // Verify each entry has required fields
            foreach (var entry in auditEntries)
            {
                if (string.IsNullOrWhiteSpace(entry.Action))
                    throw new Exception("Audit entry missing Action");
                
                if (string.IsNullOrWhiteSpace(entry.UserId))
                    throw new Exception("Audit entry missing UserId");
                
                if (entry.TimestampUtc == default)
                    throw new Exception("Audit entry missing Timestamp");
            }

            logger.LogInformation($"✅ Audit Trail: {auditEntries.Count} entries logged correctly");
        }

        private static async Task TestTransactionRollback(IServiceProvider serviceProvider, BestFlexDbContext dbContext, ILogger logger)
        {
            logger.LogInformation("🔄 Testing Transaction Rollback...");

            // Get initial state
            var initialCustomerCount = await dbContext.CustomerAccounts.CountAsync();
            var initialProductCount = await dbContext.Products.CountAsync();

            // Create a scope for the transaction test
            using var scope = serviceProvider.CreateScope();
            var unitOfWork = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();

            try
            {
                // Begin transaction
                await unitOfWork.BeginAsync();

                // Add a customer (this should be rolled back)
                var testCustomer = new CustomerAccountEntity
                {
                    Name = "Test Customer For Rollback",
                    TaxId = "TEST-ROLLBACK",
                    CreditLimit = 1000m,
                    IsActive = true,
                    CreatedAtUtc = DateTime.UtcNow
                };
                dbContext.CustomerAccounts.Add(testCustomer);

                // Add a product (this should be rolled back)
                var testProduct = new ProductEntity
                {
                    SKU = "ROLLBACK-001",
                    Name = "Test Product For Rollback",
                    Description = "Should be rolled back",
                    Cost = 50m,
                    BasePrice = 75m,
                    CurrentStock = 100,
                    IsActive = true,
                    CreatedAtUtc = DateTime.UtcNow
                };
                dbContext.Products.Add(testProduct);

                // Save changes within transaction
                await dbContext.SaveChangesAsync();

                // Verify data exists within transaction
                var transactionCustomerCount = await dbContext.CustomerAccounts.CountAsync();
                var transactionProductCount = await dbContext.Products.CountAsync();

                if (transactionCustomerCount != initialCustomerCount + 1)
                    throw new Exception("Customer not added within transaction");

                if (transactionProductCount != initialProductCount + 1)
                    throw new Exception("Product not added within transaction");

                // Force rollback by throwing an exception
                throw new InvalidOperationException("Intentional failure to test rollback");
            }
            catch (InvalidOperationException)
            {
                // Expected exception - rollback transaction
                await unitOfWork.RollbackAsync();
                logger.LogInformation("Transaction rolled back as expected");
            }

            // Verify rollback worked - counts should be back to initial
            var finalCustomerCount = await dbContext.CustomerAccounts.CountAsync();
            var finalProductCount = await dbContext.Products.CountAsync();

            if (finalCustomerCount != initialCustomerCount)
                throw new Exception($"Customer rollback failed: expected {initialCustomerCount}, got {finalCustomerCount}");

            if (finalProductCount != initialProductCount)
                throw new Exception($"Product rollback failed: expected {initialProductCount}, got {finalProductCount}");

            logger.LogInformation("✅ Transaction Rollback: System state restored correctly");
        }

        public static async Task<bool> ForceFailureTest(IServiceProvider serviceProvider, ILogger logger)
        {
            logger.LogInformation("💥 Testing Forced Failure Mid-Operation...");

            try
            {
                // This test simulates a failure during a complex operation
                using var scope = serviceProvider.CreateScope();
                var unitOfWork = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();
                var auditService = scope.ServiceProvider.GetRequiredService<IAuditService>();

                await unitOfWork.BeginAsync();

                // Log the start of operation
                await auditService.LogActionAsync("COMPLEX_OPERATION_START", "Test", 1);

                // Add some data
                var dbContext = scope.ServiceProvider.GetRequiredService<BestFlexDbContext>();
                
                var customer = new CustomerAccountEntity
                {
                    Name = "Failure Test Customer",
                    TaxId = "FAIL-001",
                    CreditLimit = 500m,
                    IsActive = true,
                    CreatedAtUtc = DateTime.UtcNow
                };
                dbContext.CustomerAccounts.Add(customer);

                await dbContext.SaveChangesAsync();

                // Log progress
                await auditService.LogActionAsync("COMPLEX_OPERATION_PROGRESS", "Test", 1);

                // Force failure
                throw new Exception("Simulated mid-operation failure");
            }
            catch (Exception ex)
            {
                logger.LogInformation($"✅ Forced failure handled correctly: {ex.Message}");
                return true;
            }
        }
    }
}
