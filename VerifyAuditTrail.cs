using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using BestFlex.Application.Abstractions;
using BestFlex.Domain.Entities;
using BestFlex.Persistence.Data;
using BestFlex.Infrastructure.Services;

namespace BestFlex.Shell
{
    /// <summary>
    /// Verification script for Audit Trail and Transaction functionality
    /// </summary>
    public static class VerifyAuditTrail
    {
        public static async Task<bool> VerifyAuditTrailCompleteness(IServiceProvider serviceProvider)
        {
            var logger = serviceProvider.GetRequiredService<ILogger<VerifyAuditTrail>>();
            var auditService = serviceProvider.GetRequiredService<IAuditService>();
            var dbContext = serviceProvider.GetRequiredService<BestFlexDbContext>();

            try
            {
                logger.LogInformation("🔍 VERIFYING AUDIT TRAIL COMPLETENESS...");

                // Clear existing entries for clean test
                dbContext.AuditEntries.RemoveRange(dbContext.AuditEntries);
                await dbContext.SaveChangesAsync();

                // Test 1: Critical Action Logging
                await auditService.LogActionAsync("CUSTOMER_CREATED", "CustomerAccount", 999);
                
                // Test 2: Security Event Logging
                await auditService.LogSecurityAsync("LOGIN_ATTEMPT", "IP: 127.0.0.1, User: admin");
                
                // Test 3: Navigation Logging
                await auditService.LogNavigationAsync("DashboardPage");

                // Test 4: Complex Action with Details
                await auditService.LogActionAsync("SALES_ORDER_CREATED", "SalesOrder", 888);

                // Verify all entries were created
                var entries = await dbContext.AuditEntries.ToListAsync();
                
                if (entries.Count != 4)
                {
                    logger.LogError($"Expected 4 audit entries, found {entries.Count}");
                    return false;
                }

                // Verify each entry has required fields
                var requiredFieldsValid = entries.All(entry => 
                    !string.IsNullOrWhiteSpace(entry.Action) &&
                    !string.IsNullOrWhiteSpace(entry.UserId) &&
                    entry.TimestampUtc != default);

                if (!requiredFieldsValid)
                {
                    logger.LogError("Some audit entries missing required fields");
                    return false;
                }

                // Verify specific actions were logged
                var actions = entries.Select(e => e.Action).ToHashSet();
                var expectedActions = new[] { "CUSTOMER_CREATED", "LOGIN_ATTEMPT", "NAVIGATION", "SALES_ORDER_CREATED" };
                
                foreach (var expectedAction in expectedActions)
                {
                    if (!actions.Contains(expectedAction))
                    {
                        logger.LogError($"Missing expected audit action: {expectedAction}");
                        return false;
                    }
                }

                logger.LogInformation("✅ Audit Trail: All critical actions logged correctly");
                logger.LogInformation($"✅ Audit Trail: {entries.Count} entries with complete data");
                
                return true;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "❌ Audit Trail Verification Failed");
                return false;
            }
        }

        public static async Task<bool> VerifyTransactionRollback(IServiceProvider serviceProvider)
        {
            var logger = serviceProvider.GetRequiredService<ILogger<VerifyAuditTrail>>();
            var dbContext = serviceProvider.GetRequiredService<BestFlexDbContext>();

            try
            {
                logger.LogInformation("🔄 VERIFYING TRANSACTION ROLLBACK...");

                // Get initial state
                var initialCustomers = await dbContext.CustomerAccounts.CountAsync();
                var initialProducts = await dbContext.Products.CountAsync();

                // Create a scope for transaction test
                using var scope = serviceProvider.CreateScope();
                var unitOfWork = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();
                var testDbContext = scope.ServiceProvider.GetRequiredService<BestFlexDbContext>();

                try
                {
                    // Begin transaction
                    await unitOfWork.BeginAsync();

                    // Add test data
                    var testCustomer = new CustomerAccountEntity
                    {
                        Name = "Rollback Test Customer",
                        TaxId = "RB-TEST-001",
                        CreditLimit = 1000m,
                        IsActive = true,
                        CreatedAtUtc = DateTime.UtcNow
                    };
                    testDbContext.CustomerAccounts.Add(testCustomer);

                    var testProduct = new ProductEntity
                    {
                        SKU = "RB-PROD-001",
                        Name = "Rollback Test Product",
                        Description = "Should be rolled back",
                        Cost = 25m,
                        BasePrice = 50m,
                        CurrentStock = 100,
                        IsActive = true,
                        CreatedAtUtc = DateTime.UtcNow
                    };
                    testDbContext.Products.Add(testProduct);

                    // Save within transaction
                    await testDbContext.SaveChangesAsync();

                    // Verify data exists within transaction
                    var transactionCustomers = await testDbContext.CustomerAccounts.CountAsync();
                    var transactionProducts = await testDbContext.Products.CountAsync();

                    if (transactionCustomers != initialCustomers + 1)
                        throw new Exception("Customer not added in transaction");
                    if (transactionProducts != initialProducts + 1)
                        throw new Exception("Product not added in transaction");

                    // Force rollback
                    throw new InvalidOperationException("Intentional rollback test");
                }
                catch (InvalidOperationException)
                {
                    // Rollback as expected
                    await unitOfWork.RollbackAsync();
                }

                // Verify rollback worked
                var finalCustomers = await dbContext.CustomerAccounts.CountAsync();
                var finalProducts = await dbContext.Products.CountAsync();

                if (finalCustomers != initialCustomers)
                {
                    logger.LogError($"Customer rollback failed: expected {initialCustomers}, got {finalCustomers}");
                    return false;
                }

                if (finalProducts != initialProducts)
                {
                    logger.LogError($"Product rollback failed: expected {initialProducts}, got {finalProducts}");
                    return false;
                }

                logger.LogInformation("✅ Transaction Rollback: System state restored correctly");
                logger.LogInformation($"✅ Transaction Rollback: Customers {initialCustomers} → {finalCustomers}");
                logger.LogInformation($"✅ Transaction Rollback: Products {initialProducts} → {finalProducts}");

                return true;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "❌ Transaction Rollback Verification Failed");
                return false;
            }
        }

        public static async Task<bool> VerifyNoPartialData(IServiceProvider serviceProvider)
        {
            var logger = serviceProvider.GetRequiredService<ILogger<VerifyAuditTrail>>();
            var dbContext = serviceProvider.GetRequiredService<BestFlexDbContext>();

            try
            {
                logger.LogInformation("🔍 VERIFYING NO PARTIAL DATA...");

                // Check for orphaned records
                var orphanedInvoices = await dbContext.SellingInvoices
                    .Where(i => !dbContext.SellingInvoiceItems.Any(ii => ii.SellingInvoiceId == i.Id))
                    .CountAsync();

                var orphanedInvoiceItems = await dbContext.SellingInvoiceItems
                    .Where(ii => !dbContext.SellingInvoices.Any(i => i.Id == ii.SellingInvoiceId))
                    .CountAsync();

                var orphanedJournalLines = await dbContext.JournalLines
                    .Where(jl => !dbContext.JournalEntries.Any(je => je.Id == jl.JournalEntryId))
                    .CountAsync();

                if (orphanedInvoices > 0)
                {
                    logger.LogError($"Found {orphanedInvoices} orphaned invoices");
                    return false;
                }

                if (orphanedInvoiceItems > 0)
                {
                    logger.LogError($"Found {orphanedInvoiceItems} orphaned invoice items");
                    return false;
                }

                if (orphanedJournalLines > 0)
                {
                    logger.LogError($"Found {orphanedJournalLines} orphaned journal lines");
                    return false;
                }

                logger.LogInformation("✅ No Partial Data: No orphaned records found");
                return true;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "❌ Partial Data Verification Failed");
                return false;
            }
        }
    }
}
