using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.EntityFrameworkCore;
using BestFlex.Application.Abstractions;
using BestFlex.Persistence.Data;

namespace BestFlex.Infrastructure.Services
{
    /// <summary>
    /// Provides startup safety checks to ensure system readiness before allowing user operations
    /// </summary>
    public class StartupSafetyService : IStartupSafetyService
    {
        private readonly ILogger<StartupSafetyService> _logger;
        private readonly IServiceProvider _serviceProvider;
        private readonly IDatabaseResilienceService _databaseResilience;

        public StartupSafetyService(
            ILogger<StartupSafetyService> logger,
            IServiceProvider serviceProvider,
            IDatabaseResilienceService databaseResilience)
        {
            _logger = logger;
            _serviceProvider = serviceProvider;
            _databaseResilience = databaseResilience;
        }

        public async Task<StartupSafetyResult> PerformSafetyChecksAsync(CancellationToken cancellationToken = default)
        {
            var result = new StartupSafetyResult();
            var correlationId = GenerateCorrelationId();

            _logger.LogInformation("Performing startup safety checks [CID:{CorrelationId}]", correlationId);

            try
            {
                // Check 1: Database connectivity
                await CheckDatabaseConnectivity(result, correlationId, cancellationToken);

                // Check 2: Schema compatibility
                await CheckSchemaCompatibility(result, correlationId, cancellationToken);

                // Check 3: Required seed data
                await CheckRequiredSeedData(result, correlationId, cancellationToken);

                // Check 4: Basic system functionality
                await CheckBasicSystemFunctionality(result, correlationId, cancellationToken);

                result.IsSafe = !result.HasErrors;
                result.CorrelationId = correlationId;

                if (result.IsSafe)
                {
                    _logger.LogInformation("Startup safety checks passed [CID:{CorrelationId}]", correlationId);
                }
                else
                {
                    _logger.LogError("Startup safety checks failed [CID:{CorrelationId}]: {Errors}", 
                        correlationId, string.Join("; ", result.Errors));
                }

                return result;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Critical error during startup safety checks [CID:{CorrelationId}]", correlationId);
                result.IsSafe = false;
                result.Errors.Add($"Critical startup error: {ex.Message}");
                result.CorrelationId = correlationId;
                return result;
            }
        }

        private async Task CheckDatabaseConnectivity(StartupSafetyResult result, string correlationId, CancellationToken cancellationToken)
        {
            try
            {
                var healthStatus = await _databaseResilience.CheckDatabaseHealthAsync(cancellationToken);
                
                if (!healthStatus.IsHealthy)
                {
                    result.Errors.Add($"Database connectivity failed: {healthStatus.ErrorMessage}");
                    result.Warnings.Add("Application may not function properly without database access");
                    _logger.LogWarning("Database connectivity check failed [CID:{CorrelationId}]: {Error}", 
                        correlationId, healthStatus.ErrorMessage);
                }
                else
                {
                    _logger.LogInformation("Database connectivity check passed [CID:{CorrelationId}] - Response time: {ResponseTime}ms", 
                        correlationId, healthStatus.ResponseTime.TotalMilliseconds);
                    
                    if (healthStatus.ResponseTime.TotalMilliseconds > 1000)
                    {
                        result.Warnings.Add($"Database response time is slow: {healthStatus.ResponseTime.TotalMilliseconds:F0}ms");
                    }
                }
            }
            catch (Exception ex)
            {
                result.Errors.Add($"Database connectivity check failed: {ex.Message}");
                _logger.LogError(ex, "Database connectivity check failed [CID:{CorrelationId}]", correlationId);
            }
        }

        private async Task CheckSchemaCompatibility(StartupSafetyResult result, string correlationId, CancellationToken cancellationToken)
        {
            try
            {
                using var scope = _serviceProvider.CreateScope();
                var dbContext = scope.ServiceProvider.GetRequiredService<BestFlexDbContext>();

                // Check if we can query core tables
                var userCount = await dbContext.Users.CountAsync(cancellationToken);
                var productCount = await dbContext.Products.CountAsync(cancellationToken);

                _logger.LogInformation("Schema compatibility check passed [CID:{CorrelationId}] - Users: {UserCount}, Products: {ProductCount}", 
                    correlationId, userCount, productCount);
            }
            catch (Exception ex)
            {
                result.Errors.Add($"Schema compatibility check failed: {ex.Message}");
                result.Warnings.Add("Database schema may be outdated or incomplete");
                _logger.LogError(ex, "Schema compatibility check failed [CID:{CorrelationId}]", correlationId);
            }
        }

        private async Task CheckRequiredSeedData(StartupSafetyResult result, string correlationId, CancellationToken cancellationToken)
        {
            try
            {
                using var scope = _serviceProvider.CreateScope();
                var dbContext = scope.ServiceProvider.GetRequiredService<BestFlexDbContext>();

                // Check for required seed data
                var userCount = await dbContext.Users.CountAsync(cancellationToken);
                
                if (userCount == 0)
                {
                    result.Errors.Add("No users found in database. System requires at least one user account.");
                    _logger.LogWarning("Seed data check failed [CID:{CorrelationId}]: No users found", correlationId);
                }
                else
                {
                    _logger.LogInformation("Seed data check passed [CID:{CorrelationId}] - Found {UserCount} users", correlationId, userCount);
                }
            }
            catch (Exception ex)
            {
                result.Errors.Add($"Seed data check failed: {ex.Message}");
                _logger.LogError(ex, "Seed data check failed [CID:{CorrelationId}]", correlationId);
            }
        }

        private async Task CheckBasicSystemFunctionality(StartupSafetyResult result, string correlationId, CancellationToken cancellationToken)
        {
            try
            {
                // Test basic DI container functionality
                using var scope = _serviceProvider.CreateScope();
                
                // Test that critical services can be resolved
                var unitOfWork = scope.ServiceProvider.GetService<IUnitOfWork>();
                var errorService = scope.ServiceProvider.GetService<IErrorService>();
                var auditService = scope.ServiceProvider.GetService<IAuditService>();

                if (unitOfWork == null)
                {
                    result.Errors.Add("UnitOfWork service not available");
                }

                if (errorService == null)
                {
                    result.Errors.Add("ErrorService not available");
                }

                if (auditService == null)
                {
                    result.Errors.Add("AuditService not available");
                }

                // Test basic logging
                await Task.Delay(10, cancellationToken); // Make truly async
                _logger.LogInformation("Basic system functionality check passed [CID:{CorrelationId}]", correlationId);
            }
            catch (Exception ex)
            {
                result.Errors.Add($"Basic system functionality check failed: {ex.Message}");
                _logger.LogError(ex, "Basic system functionality check failed [CID:{CorrelationId}]", correlationId);
            }
        }

        private string GenerateCorrelationId()
        {
            var guidStr = Guid.NewGuid().ToString("N");
            return $"STARTUP-{DateTime.UtcNow:yyyyMMdd-HHmmss}-{guidStr.Substring(0, 8).ToUpper()}";
        }
    }

    public interface IStartupSafetyService
    {
        Task<StartupSafetyResult> PerformSafetyChecksAsync(CancellationToken cancellationToken = default);
    }

    public class StartupSafetyResult
    {
        public bool IsSafe { get; set; }
        public List<string> Errors { get; set; } = new();
        public List<string> Warnings { get; set; } = new();
        public string? CorrelationId { get; set; }
        public bool HasErrors => Errors.Count > 0;
        public bool HasWarnings => Warnings.Count > 0;

        public string GetSummaryMessage()
        {
            if (IsSafe)
            {
                var message = "System is ready for use";
                if (HasWarnings)
                {
                    message += $" (with {Warnings.Count} warnings)";
                }
                return message;
            }
            else
            {
                return $"System is not ready: {Errors.Count} critical issues found";
            }
        }
    }
}
