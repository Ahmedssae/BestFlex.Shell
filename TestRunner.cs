using System;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using BestFlex.Shell.Tests;

namespace BestFlex.Shell
{
    /// <summary>
    /// Test runner for audit and transaction verification
    /// </summary>
    public static class TestRunner
    {
        public static async Task<bool> RunAllTests(IServiceProvider serviceProvider)
        {
            var logger = serviceProvider.GetRequiredService<ILogger<TestRunner>>();
            
            try
            {
                logger.LogInformation("🚀 STARTING COMPREHENSIVE AUDIT & TRANSACTION TESTS");

                // Test 1: Audit Trail Verification
                var auditResult = await AuditTransactionTest.RunAuditTrailTest(serviceProvider);
                if (!auditResult)
                {
                    logger.LogError("❌ Audit Trail Test Failed");
                    return false;
                }

                // Test 2: Forced Failure Test
                var failureResult = await AuditTransactionTest.ForceFailureTest(serviceProvider, logger);
                if (!failureResult)
                {
                    logger.LogError("❌ Forced Failure Test Failed");
                    return false;
                }

                logger.LogInformation("🎉 ALL TESTS COMPLETED SUCCESSFULLY");
                return true;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "❌ TEST RUNNER FAILED");
                return false;
            }
        }
    }
}
