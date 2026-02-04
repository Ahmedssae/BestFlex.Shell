using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using BestFlex.Application.Abstractions;

namespace BestFlex.Shell.Services
{
    /// <summary>
    /// Enforces release safety rules for production deployments
    /// </summary>
    public class ReleaseSafetyService : IReleaseSafetyService
    {
        private readonly ILogger<ReleaseSafetyService> _logger;
        private readonly IStructuredLoggingService _structuredLogger;
        private readonly ICorrelationService _correlationService;
        private readonly IVersioningService _versioningService;
        private readonly IEnvironmentService _environmentService;
        private readonly IDatabaseMigrationService _migrationService;
        private readonly IBackupRollbackService _backupService;
        private readonly IInstallationService _installationService;
        private readonly ICurrentUserService _currentUserService;

        public ReleaseSafetyService(
            ILogger<ReleaseSafetyService> logger,
            IStructuredLoggingService structuredLogger,
            ICorrelationService correlationService,
            IVersioningService versioningService,
            IEnvironmentService environmentService,
            IDatabaseMigrationService migrationService,
            IBackupRollbackService backupService,
            IInstallationService installationService,
            ICurrentUserService currentUserService)
        {
            _logger = logger;
            _structuredLogger = structuredLogger;
            _correlationService = correlationService;
            _versioningService = versioningService;
            _environmentService = environmentService;
            _migrationService = migrationService;
            _backupService = backupService;
            _installationService = installationService;
            _currentUserService = currentUserService;
        }

        public async Task<ReleaseSafetyCheckResult> PerformReleaseSafetyCheck()
        {
            try
            {
                var result = new ReleaseSafetyCheckResult
                {
                    IsSafe = true,
                    CheckTime = DateTime.UtcNow,
                    CurrentVersion = _versioningService.GetCurrentVersion(),
                    Environment = await _environmentService.GetCurrentEnvironment()
                };

                // Check 1: No destructive operations without backup
                await CheckDestructiveOperationSafety(result);

                // Check 2: Upgrade requires successful backup
                await CheckUpgradeBackupRequirement(result);

                // Check 3: Downgrade detection and prevention
                await CheckDowngradePrevention(result);

                // Check 4: Database compatibility
                await CheckDatabaseCompatibility(result);

                // Check 5: Environment-specific safety rules
                await CheckEnvironmentSpecificSafety(result);

                // Check 6: Configuration integrity
                await CheckConfigurationIntegrity(result);

                // Check 7: Installation validation
                await CheckInstallationValidation(result);

                // Overall safety assessment
                result.IsSafe = !result.SafetyViolations.Any(v => v.Severity == SafetyViolationSeverity.Critical);

                _structuredLogger.LogSystemEvent("ReleaseSafetyCheck", 
                    result.IsSafe ? "Success" : "Failed", 
                    result.IsSafe ? "Release safety check passed" : $"Safety violations: {result.SafetyViolations.Count}");

                return result;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[RELEASE_SAFETY_ERROR] Error performing release safety check");
                
                return new ReleaseSafetyCheckResult
                {
                    IsSafe = false,
                    ErrorMessage = $"Release safety check failed: {ex.Message}"
                };
            }
        }

        public async Task<bool> CanPerformDestructiveOperation(string operationType, Dictionary<string, object>? operationContext = null)
        {
            try
            {
                // Check if backup is required
                var env = await _environmentService.GetCurrentEnvironment();
                var requiresBackup = env.Type == EnvironmentType.Production;

                if (requiresBackup)
                {
                    // Check if recent backup exists
                    var backupHealth = await _backupService.GetBackupHealthStatus();
                    var lastBackup = backupHealth.LastBackupTime;
                    
                    if (lastBackup == null || lastBackup < DateTime.UtcNow.AddHours(-24))
                    {
                        _logger.LogWarning("[DESTRUCTIVE_OPERATION_BLOCKED] No recent backup found for destructive operation: {Operation}", operationType);
                        return false;
                    }

                    // Verify backup integrity
                    var recentBackups = await _backupService.GetRecentBackups(3);
                    if (!recentBackups.Any())
                    {
                        _logger.LogWarning("[DESTRUCTIVE_OPERATION_BLOCKED] No backup history found for destructive operation: {Operation}", operationType);
                        return false;
                    }
                }

                // Log destructive operation attempt
                _structuredLogger.LogUserAction("DestructiveOperationAttempt", "System", new Dictionary<string, object>
                {
                    ["OperationType"] = operationType,
                    ["RequiresBackup"] = requiresBackup,
                    ["Environment"] = env.Name,
                    ["OperationContext"] = operationContext != null ? JsonSerializer.Serialize(operationContext) : "{}"
                });

                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[DESTRUCTIVE_OPERATION_CHECK_ERROR] Error checking destructive operation safety");
                return false;
            }
        }

        public async Task<UpgradeSafetyResult> CheckUpgradeSafety(VersionInfo targetVersion)
        {
            try
            {
                var result = new UpgradeSafetyResult
                {
                    IsSafe = true,
                    CurrentVersion = _versioningService.GetCurrentVersion(),
                    TargetVersion = targetVersion
                };

                // Check if this is actually an upgrade
                if (targetVersion.Major < result.CurrentVersion.Major ||
                    (targetVersion.Major == result.CurrentVersion.Major && targetVersion.Minor < result.CurrentVersion.Minor))
                {
                    result.IsSafe = false;
                    result.Violations.Add(new SafetyViolation
                    {
                        Type = SafetyViolationType.DowngradeAttempt,
                        Severity = SafetyViolationSeverity.Critical,
                        Message = "Attempted downgrade detected",
                        Recommendation = "Downgrades are not supported in production"
                    });
                    return result;
                }

                // Check if backup is required and available
                var backupHealth = await _backupService.GetBackupHealthStatus();
                if (backupHealth.HealthLevel == BackupHealthLevel.Critical)
                {
                    result.IsSafe = false;
                    result.Violations.Add(new SafetyViolation
                    {
                        Type = SafetyViolationType.NoBackup,
                        Severity = SafetyViolationSeverity.Critical,
                        Message = "No backup available for upgrade",
                        Recommendation = "Create a backup before performing upgrade"
                    });
                }

                // Check database compatibility
                var dbCompatibility = await _migrationService.CheckDatabaseCompatibility();
                if (!dbCompatibility.IsCompatible)
                {
                    result.IsSafe = false;
                    result.Violations.Add(new SafetyViolation
                    {
                        Type = SafetyViolationType.DatabaseIncompatible,
                        Severity = SafetyViolationSeverity.Critical,
                        Message = dbCompatibility.BlockReason ?? "Database compatibility issue",
                        Recommendation = dbCompatibility.RequiredAction ?? "Contact database administrator"
                    });
                }

                // Check environment-specific upgrade rules
                await CheckUpgradeEnvironmentRules(result);

                return result;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[UPGRADE_SAFETY_ERROR] Error checking upgrade safety");
                
                return new UpgradeSafetyResult
                {
                    IsSafe = false,
                    ErrorMessage = $"Upgrade safety check failed: {ex.Message}"
                };
            }
        }

        public async Task<bool> PreventDowngrade(VersionInfo targetVersion)
        {
            try
            {
                var currentVersion = _versioningService.GetCurrentVersion();
                var env = await _environmentService.GetCurrentEnvironment();

                // Allow downgrade in development environment
                if (env.Type == EnvironmentType.Development)
                {
                    _logger.LogInformation("[DOWNGRADE_ALLOWED] Downgrade allowed in development environment");
                    return true;
                }

                // Check if target version is older than current
                if (targetVersion.Major < currentVersion.Major ||
                    (targetVersion.Major == currentVersion.Major && targetVersion.Minor < currentVersion.Minor))
                {
                    // Check if downgrade is safe (no irreversible migrations)
                    var canDowngrade = await _migrationService.CanDowngrade(targetVersion);
                    if (!canDowngrade)
                    {
                        _logger.LogWarning("[DOWNGRADE_BLOCKED] Downgrade blocked due to irreversible migrations");
                        return false;
                    }

                    // In production, require explicit confirmation for downgrade
                    if (env.Type == EnvironmentType.Production)
                    {
                        _logger.LogWarning("[DOWNGRADE_BLOCKED] Downgrade not allowed in production environment");
                        return false;
                    }
                }

                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[DOWNGRADE_PREVENTION_ERROR] Error preventing downgrade");
                return false;
            }
        }

        public async Task<ReleaseSafetyReport> GenerateReleaseSafetyReport()
        {
            try
            {
                var report = new ReleaseSafetyReport
                {
                    GeneratedAt = DateTime.UtcNow,
                    GeneratedBy = _currentUserService.Username,
                    CurrentVersion = _versioningService.GetCurrentVersion(),
                    Environment = await _environmentService.GetCurrentEnvironment()
                };

                // Perform comprehensive safety check
                var safetyCheck = await PerformReleaseSafetyCheck();
                report.SafetyCheck = safetyCheck;

                // Get backup status
                report.BackupStatus = await _backupService.GetBackupHealthStatus();

                // Get database status
                report.DatabaseStatus = await _migrationService.CheckDatabaseCompatibility();

                // Get installation status
                report.InstallationStatus = await _installationService.GetInstallationStatus();

                // Get configuration status
                // Note: This would require IConfigurationService injection
                // report.ConfigurationStatus = await _configurationService.GetConfigurationStatus();

                // Calculate overall safety score
                report.OverallSafetyScore = CalculateSafetyScore(report);

                _structuredLogger.LogUserAction("ReleaseSafetyReportGenerated", "System", new Dictionary<string, object>
                {
                    ["ReportId"] = report.ReportId,
                    ["SafetyScore"] = report.OverallSafetyScore,
                    ["Environment"] = report.Environment.Name
                });

                return report;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[RELEASE_SAFETY_REPORT_ERROR] Error generating release safety report");
                
                return new ReleaseSafetyReport
                {
                    GeneratedAt = DateTime.UtcNow,
                    ErrorMessage = $"Failed to generate safety report: {ex.Message}"
                };
            }
        }

        private async Task CheckDestructiveOperationSafety(ReleaseSafetyCheckResult result)
        {
            try
            {
                // Check if any destructive operations are pending
                var env = await _environmentService.GetCurrentEnvironment();
                
                if (env.Type == EnvironmentType.Production)
                {
                    var backupHealth = await _backupService.GetBackupHealthStatus();
                    
                    if (backupHealth.HealthLevel == BackupHealthLevel.Critical)
                    {
                        result.SafetyViolations.Add(new SafetyViolation
                        {
                            Type = SafetyViolationType.NoBackup,
                            Severity = SafetyViolationSeverity.Critical,
                            Message = "No backup available for destructive operations",
                            Recommendation = "Create a backup before performing destructive operations"
                        });
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[DESTRUCTIVE_SAFETY_CHECK_ERROR] Error checking destructive operation safety");
                
                result.SafetyViolations.Add(new SafetyViolation
                {
                    Type = SafetyViolationType.SystemError,
                    Severity = SafetyViolationSeverity.Warning,
                    Message = "Error checking destructive operation safety",
                    Recommendation = "Review system logs for details"
                });
            }
        }

        private async Task CheckUpgradeBackupRequirement(ReleaseSafetyCheckResult result)
        {
            try
            {
                var backupHealth = await _backupService.GetBackupHealthStatus();
                
                if (backupHealth.HealthLevel == BackupHealthLevel.Critical)
                {
                    result.SafetyViolations.Add(new SafetyViolation
                    {
                        Type = SafetyViolationType.NoBackup,
                        Severity = SafetyViolationSeverity.Critical,
                        Message = "No backup available for upgrade",
                        Recommendation = "Create a backup before performing upgrade"
                    });
                }
                else if (backupHealth.HealthLevel == BackupHealthLevel.Warning)
                {
                    result.SafetyViolations.Add(new SafetyViolation
                    {
                        Type = SafetyViolationType.BackupWarning,
                        Severity = SafetyViolationSeverity.Warning,
                        Message = "Backup system has warnings",
                        Recommendation = "Review backup system health before upgrade"
                    });
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[UPGRADE_BACKUP_CHECK_ERROR] Error checking upgrade backup requirement");
                
                result.SafetyViolations.Add(new SafetyViolation
                {
                    Type = SafetyViolationType.SystemError,
                    Severity = SafetyViolationSeverity.Warning,
                    Message = "Error checking backup requirements",
                    Recommendation = "Verify backup system manually"
                });
            }
        }

        private async Task CheckDowngradePrevention(ReleaseSafetyCheckResult result)
        {
            try
            {
                var currentVersion = result.CurrentVersion;
                var env = result.Environment;

                // In production, downgrade should be prevented
                if (env.Type == EnvironmentType.Production)
                {
                    result.SafetyViolations.Add(new SafetyViolation
                    {
                        Type = SafetyViolationType.DowngradePrevention,
                        Severity = SafetyViolationSeverity.Warning,
                        Message = "Downgrade prevention is active in production",
                        Recommendation = "Downgrades require explicit override in production"
                    });
                }

                // Check for irreversible migrations
                var canDowngrade = await _migrationService.CanDowngrade(new VersionInfo { Major = currentVersion.Major - 1, Minor = 0, Patch = 0 });
                if (!canDowngrade)
                {
                    result.SafetyViolations.Add(new SafetyViolation
                    {
                        Type = SafetyViolationType.IrreversibleMigrations,
                        Severity = SafetyViolationSeverity.Critical,
                        Message = "Irreversible migrations prevent downgrade",
                        Recommendation = "Downgrade not possible due to irreversible database changes"
                    });
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[DOWNGRADE_PREVENTION_CHECK_ERROR] Error checking downgrade prevention");
                
                result.SafetyViolations.Add(new SafetyViolation
                {
                    Type = SafetyViolationType.SystemError,
                    Severity = SafetyViolationSeverity.Warning,
                    Message = "Error checking downgrade prevention",
                    Recommendation = "Review migration logs"
                });
            }
        }

        private async Task CheckDatabaseCompatibility(ReleaseSafetyCheckResult result)
        {
            try
            {
                var dbCompatibility = await _migrationService.CheckDatabaseCompatibility();
                
                if (!dbCompatibility.IsCompatible)
                {
                    result.SafetyViolations.Add(new SafetyViolation
                    {
                        Type = SafetyViolationType.DatabaseIncompatible,
                        Severity = SafetyViolationSeverity.Critical,
                        Message = dbCompatibility.BlockReason ?? "Database compatibility issue",
                        Recommendation = dbCompatibility.RequiredAction ?? "Contact database administrator"
                    });
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[DATABASE_COMPATIBILITY_CHECK_ERROR] Error checking database compatibility");
                
                result.SafetyViolations.Add(new SafetyViolation
                {
                    Type = SafetyViolationType.SystemError,
                    Severity = SafetyViolationSeverity.Warning,
                    Message = "Error checking database compatibility",
                    Recommendation = "Verify database connection manually"
                });
            }
        }

        private async Task CheckEnvironmentSpecificSafety(ReleaseSafetyCheckResult result)
        {
            try
            {
                var env = result.Environment;
                
                switch (env.Type)
                {
                    case EnvironmentType.Production:
                        // Production requires HTTPS
                        if (!env.RequireHttps)
                        {
                            result.SafetyViolations.Add(new SafetyViolation
                            {
                                Type = SafetyViolationType.SecurityViolation,
                                Severity = SafetyViolationSeverity.Critical,
                                Message = "HTTPS is not required in production",
                                Recommendation = "Enable HTTPS for production environment"
                            });
                        }
                        break;

                    case EnvironmentType.Staging:
                        // Staging should not use production database
                        var dbConnection = await _environmentService.GetCurrentEnvironment();
                        if (dbConnection.DatabaseConnectionString.Contains("bestflex_prod"))
                        {
                            result.SafetyViolations.Add(new SafetyViolation
                            {
                                Type = SafetyViolationType.ConfigurationViolation,
                                Severity = SafetyViolationSeverity.Warning,
                                Message = "Staging environment using production database",
                                Recommendation = "Use staging-specific database connection"
                            });
                        }
                        break;

                    case EnvironmentType.Development:
                        // Development environment safety checks are more lenient
                        break;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[ENVIRONMENT_SAFETY_CHECK_ERROR] Error checking environment-specific safety");
                
                result.SafetyViolations.Add(new SafetyViolation
                {
                    Type = SafetyViolationType.SystemError,
                    Severity = SafetyViolationSeverity.Warning,
                    Message = "Error checking environment-specific safety",
                    Recommendation = "Review environment configuration"
                });
            }
        }

        private async Task CheckConfigurationIntegrity(ReleaseSafetyCheckResult result)
        {
            try
            {
                // Check if configuration files exist and are valid
                var configFiles = new[] { "appsettings.json", "appsettings.development.json", "appsettings.staging.json", "appsettings.production.json" };
                
                foreach (var configFile in configFiles)
                {
                    if (File.Exists(configFile))
                    {
                        try
                        {
                            var content = await File.ReadAllTextAsync(configFile);
                            JsonDocument.Parse(content); // Validate JSON
                        }
                        catch (JsonException ex)
                        {
                            result.SafetyViolations.Add(new SafetyViolation
                            {
                                Type = SafetyViolationType.ConfigurationViolation,
                                Severity = SafetyViolationSeverity.Critical,
                                Message = $"Invalid JSON in {configFile}: {ex.Message}",
                                Recommendation = "Fix configuration file format"
                            });
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[CONFIG_INTEGRITY_CHECK_ERROR] Error checking configuration integrity");
                
                result.SafetyViolations.Add(new SafetyViolation
                {
                    Type = SafetyViolationType.SystemError,
                    Severity = SafetyViolationSeverity.Warning,
                    Message = "Error checking configuration integrity",
                    Recommendation = "Verify configuration files manually"
                });
            }
        }

        private async Task CheckInstallationValidation(ReleaseSafetyCheckResult result)
        {
            try
            {
                var installationStatus = await _installationService.GetInstallationStatus();
                
                if (!installationStatus.IsInstalled)
                {
                    result.SafetyViolations.Add(new SafetyViolation
                    {
                        Type = SafetyViolationType.InstallationIssue,
                        Severity = SafetyViolationSeverity.Critical,
                        Message = "Application not properly installed",
                        Recommendation = "Complete first-run setup"
                    });
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[INSTALLATION_VALIDATION_CHECK_ERROR] Error checking installation validation");
                
                result.SafetyViolations.Add(new SafetyViolation
                {
                    Type = SafetyViolationType.SystemError,
                    Severity = SafetyViolationSeverity.Warning,
                    Message = "Error checking installation status",
                    Recommendation = "Verify installation manually"
                });
            }
        }

        private async Task CheckUpgradeEnvironmentRules(UpgradeSafetyResult result)
        {
            try
            {
                var env = await _environmentService.GetCurrentEnvironment();
                
                switch (env.Type)
                {
                    case EnvironmentType.Production:
                        // Production upgrades require additional safety checks
                        result.Violations.Add(new SafetyViolation
                        {
                            Type = SafetyViolationType.ProductionUpgrade,
                            Severity = SafetyViolationSeverity.Warning,
                            Message = "Production upgrade requires additional precautions",
                            Recommendation = "Schedule maintenance window and notify users"
                        });
                        break;

                    case EnvironmentType.Staging:
                        // Staging upgrades should be tested thoroughly
                        result.Violations.Add(new SafetyViolation
                        {
                            Type = SafetyViolationType.StagingUpgrade,
                            Severity = SafetyViolationSeverity.Info,
                            Message = "Staging upgrade detected",
                            Recommendation = "Test thoroughly before production deployment"
                        });
                        break;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[UPGRADE_ENV_RULES_ERROR] Error checking upgrade environment rules");
                
                result.Violations.Add(new SafetyViolation
                {
                    Type = SafetyViolationType.SystemError,
                    Severity = SafetyViolationSeverity.Warning,
                    Message = "Error checking upgrade environment rules",
                    Recommendation = "Review environment configuration"
                });
            }
        }

        private int CalculateSafetyScore(ReleaseSafetyReport report)
        {
            var score = 100; // Start with perfect score
            
            // Deduct points for safety violations
            foreach (var violation in report.SafetyCheck.SafetyViolations)
            {
                switch (violation.Severity)
                {
                    case SafetyViolationSeverity.Critical:
                        score -= 25;
                        break;
                    case SafetyViolationSeverity.Warning:
                        score -= 10;
                        break;
                    case SafetyViolationSeverity.Info:
                        score -= 5;
                        break;
                }
            }
            
            // Deduct points for backup health issues
            switch (report.BackupStatus.HealthLevel)
            {
                case BackupHealthLevel.Critical:
                    score -= 20;
                    break;
                case BackupHealthLevel.Warning:
                    score -= 10;
                    break;
            }
            
            // Deduct points for database compatibility issues
            if (!report.DatabaseStatus.IsCompatible)
            {
                score -= 30;
            }
            
            return Math.Max(0, score);
        }
    }

    public interface IReleaseSafetyService
    {
        Task<ReleaseSafetyCheckResult> PerformReleaseSafetyCheck();
        Task<bool> CanPerformDestructiveOperation(string operationType, Dictionary<string, object>? operationContext = null);
        Task<UpgradeSafetyResult> CheckUpgradeSafety(VersionInfo targetVersion);
        Task<bool> PreventDowngrade(VersionInfo targetVersion);
        Task<ReleaseSafetyReport> GenerateReleaseSafetyReport();
    }

    // Data classes
    public class ReleaseSafetyCheckResult
    {
        public bool IsSafe { get; set; }
        public DateTime CheckTime { get; set; }
        public VersionInfo CurrentVersion { get; set; } = new();
        public EnvironmentConfig Environment { get; set; } = new();
        public List<SafetyViolation> SafetyViolations { get; set; } = new();
        public string? ErrorMessage { get; set; }
    }

    public class UpgradeSafetyResult
    {
        public bool IsSafe { get; set; }
        public VersionInfo CurrentVersion { get; set; } = new();
        public VersionInfo TargetVersion { get; set; } = new();
        public List<SafetyViolation> Violations { get; set; } = new();
        public string? ErrorMessage { get; set; }
    }

    public class SafetyViolation
    {
        public SafetyViolationType Type { get; set; }
        public SafetyViolationSeverity Severity { get; set; }
        public string Message { get; set; } = string.Empty;
        public string Recommendation { get; set; } = string.Empty;
    }

    public class ReleaseSafetyReport
    {
        public string ReportId { get; set; } = Guid.NewGuid().ToString("N");
        public DateTime GeneratedAt { get; set; }
        public string GeneratedBy { get; set; } = string.Empty;
        public VersionInfo CurrentVersion { get; set; } = new();
        public EnvironmentConfig Environment { get; set; } = new();
        public ReleaseSafetyCheckResult SafetyCheck { get; set; } = new();
        public BackupHealthStatus BackupStatus { get; set; } = new();
        public MigrationCheckResult DatabaseStatus { get; set; } = new();
        public InstallationStatus InstallationStatus { get; set; } = new();
        public int OverallSafetyScore { get; set; }
        public string? ErrorMessage { get; set; }
    }

    public enum SafetyViolationType
    {
        NoBackup,
        DowngradeAttempt,
        DowngradePrevention,
        IrreversibleMigrations,
        DatabaseIncompatible,
        SecurityViolation,
        ConfigurationViolation,
        InstallationIssue,
        SystemError,
        ProductionUpgrade,
        StagingUpgrade,
        BackupWarning
    }

    public enum SafetyViolationSeverity
    {
        Info,
        Warning,
        Critical
    }
}
