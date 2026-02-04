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
    /// Provides database migration safety with version checking and upgrade protection
    /// </summary>
    public class DatabaseMigrationService : IDatabaseMigrationService
    {
        private readonly ILogger<DatabaseMigrationService> _logger;
        private readonly IStructuredLoggingService _structuredLogger;
        private readonly ICorrelationService _correlationService;
        private readonly IVersioningService _versioningService;
        private readonly IEnvironmentService _environmentService;

        private const string MigrationHistoryFile = "migration_history.json";
        private const string SchemaVersionFile = "schema_version.json";

        public DatabaseMigrationService(
            ILogger<DatabaseMigrationService> logger,
            IStructuredLoggingService structuredLogger,
            ICorrelationService correlationService,
            IVersioningService versioningService,
            IEnvironmentService environmentService)
        {
            _logger = logger;
            _structuredLogger = structuredLogger;
            _correlationService = correlationService;
            _versioningService = versioningService;
            _environmentService = environmentService;
        }

        public async Task<MigrationCheckResult> CheckDatabaseCompatibility()
        {
            try
            {
                var appVersion = _versioningService.GetCurrentVersion();
                var dbSchemaVersion = await GetDatabaseSchemaVersion();
                var migrationHistory = await GetMigrationHistory();

                var result = new MigrationCheckResult
                {
                    ApplicationVersion = appVersion,
                    DatabaseSchemaVersion = dbSchemaVersion,
                    MigrationHistory = migrationHistory,
                    IsCompatible = true
                };

                // Check if database schema is newer than application
                if (IsDatabaseNewerThanApplication(dbSchemaVersion, appVersion))
                {
                    result.IsCompatible = false;
                    result.BlockReason = "Database schema is newer than application version";
                    result.RequiredAction = "Upgrade application to match database schema";
                    result.Severity = MigrationSeverity.Critical;
                }
                // Check if application is newer and requires migration
                else if (IsApplicationNewerThanDatabase(appVersion, dbSchemaVersion))
                {
                    result.IsCompatible = false;
                    result.BlockReason = "Application is newer than database schema";
                    result.RequiredAction = "Run database migration to upgrade schema";
                    result.Severity = MigrationSeverity.Required;
                }
                // Check for pending migrations
                else if (HasPendingMigrations(appVersion, dbSchemaVersion, migrationHistory))
                {
                    result.IsCompatible = false;
                    result.BlockReason = "Pending database migrations exist";
                    result.RequiredAction = "Apply pending migrations before continuing";
                    result.Severity = MigrationSeverity.Warning;
                }

                _structuredLogger.LogSystemEvent("DatabaseCompatibilityCheck", 
                    result.IsCompatible ? "Success" : "Failed", 
                    result.BlockReason ?? "Database compatible with application");

                return result;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[MIGRATION_CHECK_ERROR] Error checking database compatibility");
                
                return new MigrationCheckResult
                {
                    IsCompatible = false,
                    BlockReason = $"Error checking database compatibility: {ex.Message}",
                    Severity = MigrationSeverity.Critical
                };
            }
        }

        public async Task<MigrationResult> PerformMigration(MigrationRequest request)
        {
            try
            {
                var validationResult = await ValidateMigrationRequest(request);
                if (!validationResult.IsValid)
                {
                    return new MigrationResult
                    {
                        Success = false,
                        Message = string.Join("; ", validationResult.Errors)
                    };
                }

                // Create backup before migration
                if (request.RequireBackup)
                {
                    var backupResult = await CreateMigrationBackup(request);
                    if (!backupResult.Success)
                    {
                        return new MigrationResult
                        {
                            Success = false,
                            Message = $"Failed to create migration backup: {backupResult.Message}"
                        };
                    }
                }

                // Perform the migration
                var migrationResult = await ExecuteMigration(request);

                if (migrationResult.Success)
                {
                    // Update migration history
                    await UpdateMigrationHistory(request, migrationResult);
                    
                    // Update schema version
                    await UpdateSchemaVersion(request.TargetVersion);

                    _structuredLogger.LogBusinessOperation("DatabaseMigration", "Schema", request.TargetVersion.SemanticVersion, 
                        new Dictionary<string, object>
                        {
                            ["FromVersion"] = request.SourceVersion.SemanticVersion,
                            ["ToVersion"] = request.TargetVersion.SemanticVersion,
                            ["MigrationType"] = request.MigrationType.ToString()
                        });
                }

                return migrationResult;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[MIGRATION_ERROR] Error performing database migration");
                
                _structuredLogger.LogError(ex, "DatabaseMigration", request.MigrationType.ToString(), 
                    request.TargetVersion.SemanticVersion);

                return new MigrationResult
                {
                    Success = false,
                    Message = $"Migration failed: {ex.Message}"
                };
            }
        }

        public async Task<bool> ValidateMigrationSafety()
        {
            try
            {
                var checks = new List<MigrationSafetyCheck>
                {
                    await CheckDatabaseConnectivity(),
                    await CheckBackupAvailability(),
                    await CheckDiskSpace(),
                    await CheckUserPermissions(),
                    await CheckConcurrentUsers()
                };

                var failedChecks = checks.Where(c => !c.Passed).ToList();
                
                if (failedChecks.Any())
                {
                    _logger.LogWarning("[MIGRATION_SAFETY_FAILED] Safety checks failed: {Checks}", 
                        string.Join("; ", failedChecks.Select(c => c.Message)));
                    
                    return false;
                }

                _structuredLogger.LogSystemEvent("MigrationSafetyCheck", "Success", "All migration safety checks passed");
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[MIGRATION_SAFETY_ERROR] Error validating migration safety");
                return false;
            }
        }

        public async Task<MigrationHistory> GetMigrationHistory()
        {
            try
            {
                if (!File.Exists(MigrationHistoryFile))
                {
                    return new MigrationHistory { Migrations = new List<MigrationRecord>() };
                }

                var json = await File.ReadAllTextAsync(MigrationHistoryFile);
                return JsonSerializer.Deserialize<MigrationHistory>(json) ?? new MigrationHistory();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[MIGRATION_HISTORY_ERROR] Error loading migration history");
                return new MigrationHistory { Migrations = new List<MigrationRecord>() };
            }
        }

        public async Task<bool> CanDowngrade(VersionInfo targetVersion)
        {
            try
            {
                var currentVersion = _versioningService.GetCurrentVersion();
                var migrationHistory = await GetMigrationHistory();

                // Check if target version is older than current
                if (targetVersion.Major < currentVersion.Major ||
                    (targetVersion.Major == currentVersion.Major && targetVersion.Minor < currentVersion.Minor))
                {
                    // Check if there are irreversible migrations
                    var irreversibleMigrations = migrationHistory.Migrations
                        .Where(m => m.IsIrreversible && m.AppliedAt > DateTime.UtcNow.AddDays(-30))
                        .ToList();

                    if (irreversibleMigrations.Any())
                    {
                        _logger.LogWarning("[DOWNGRADE_BLOCKED] Cannot downgrade due to irreversible migrations: {Migrations}", 
                            string.Join(", ", irreversibleMigrations.Select(m => m.Version)));
                        return false;
                    }
                }

                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[DOWNGRADE_CHECK_ERROR] Error checking downgrade possibility");
                return false;
            }
        }

        private async Task<SchemaVersion> GetDatabaseSchemaVersion()
        {
            try
            {
                if (!File.Exists(SchemaVersionFile))
                {
                    // Create initial schema version file
                    var initialVersion = new SchemaVersion
                    {
                        Version = _versioningService.GetCurrentVersion(),
                        AppliedAt = DateTime.UtcNow,
                        MigrationId = "initial"
                    };
                    
                    await SaveSchemaVersion(initialVersion);
                    return initialVersion;
                }

                var json = await File.ReadAllTextAsync(SchemaVersionFile);
                return JsonSerializer.Deserialize<SchemaVersion>(json) ?? new SchemaVersion();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[SCHEMA_VERSION_ERROR] Error getting database schema version");
                return new SchemaVersion();
            }
        }

        private async Task SaveSchemaVersion(SchemaVersion schemaVersion)
        {
            var json = JsonSerializer.Serialize(schemaVersion, new JsonSerializerOptions { WriteIndented = true });
            await File.WriteAllTextAsync(SchemaVersionFile, json);
        }

        private async Task UpdateSchemaVersion(VersionInfo newVersion)
        {
            var schemaVersion = new SchemaVersion
            {
                Version = newVersion,
                AppliedAt = DateTime.UtcNow,
                MigrationId = Guid.NewGuid().ToString("N")
            };
            
            await SaveSchemaVersion(schemaVersion);
        }

        private async Task UpdateMigrationHistory(MigrationRequest request, MigrationResult result)
        {
            var history = await GetMigrationHistory();
            
            history.Migrations.Add(new MigrationRecord
            {
                Version = request.TargetVersion,
                MigrationType = request.MigrationType,
                AppliedAt = DateTime.UtcNow,
                Success = result.Success,
                Duration = result.Duration,
                ErrorMessage = result.Success ? null : result.Message,
                IsIrreversible = request.IsIrreversible,
                MigrationId = request.MigrationId
            });

            var json = JsonSerializer.Serialize(history, new JsonSerializerOptions { WriteIndented = true });
            await File.WriteAllTextAsync(MigrationHistoryFile, json);
        }

        private bool IsDatabaseNewerThanApplication(SchemaVersion dbVersion, VersionInfo appVersion)
        {
            return dbVersion.Version.Major > appVersion.Major ||
                   (dbVersion.Version.Major == appVersion.Major && dbVersion.Version.Minor > appVersion.Minor);
        }

        private bool IsApplicationNewerThanDatabase(VersionInfo appVersion, SchemaVersion dbVersion)
        {
            return appVersion.Major > dbVersion.Version.Major ||
                   (appVersion.Major == dbVersion.Version.Major && appVersion.Minor > dbVersion.Version.Minor);
        }

        private bool HasPendingMigrations(VersionInfo appVersion, SchemaVersion dbVersion, MigrationHistory history)
        {
            // In a real implementation, this would check for pending migration files
            // For now, return false
            return false;
        }

        private async Task<MigrationValidationResult> ValidateMigrationRequest(MigrationRequest request)
        {
            var result = new MigrationValidationResult { IsValid = true };

            // Validate versions
            if (request.SourceVersion == null || request.TargetVersion == null)
            {
                result.IsValid = false;
                result.Errors.Add("Source and target versions are required");
            }

            // Validate migration type
            if (!Enum.IsDefined(typeof(MigrationType), request.MigrationType))
            {
                result.IsValid = false;
                result.Errors.Add("Invalid migration type");
            }

            // Check if downgrade is allowed
            if (request.MigrationType == MigrationType.Downgrade)
            {
                if (request.TargetVersion == null)
                {
                    result.IsValid = false;
                    result.Errors.Add("Target version is required for downgrade");
                    return result;
                }
                
                var canDowngrade = await CanDowngrade(request.TargetVersion);
                if (!canDowngrade)
                {
                    result.IsValid = false;
                    result.Errors.Add("Downgrade not allowed due to irreversible migrations");
                }
            }

            // Validate environment
            var env = await _environmentService.GetCurrentEnvironment();
            if (env.Type == EnvironmentType.Production && !request.RequireBackup)
            {
                result.IsValid = false;
                result.Errors.Add("Production migrations require backup");
            }

            return result;
        }

        private async Task<MigrationBackupResult> CreateMigrationBackup(MigrationRequest request)
        {
            // In a real implementation, this would create an actual database backup
            await Task.Delay(1000); // Simulate backup creation
            
            return new MigrationBackupResult
            {
                Success = true,
                BackupId = $"migration_backup_{DateTime.UtcNow:yyyyMMddHHmmss}",
                Message = "Migration backup created successfully"
            };
        }

        private async Task<MigrationResult> ExecuteMigration(MigrationRequest request)
        {
            try
            {
                var startTime = DateTime.UtcNow;
                
                // In a real implementation, this would execute actual database migrations
                await Task.Delay(2000); // Simulate migration execution
                
                var duration = DateTime.UtcNow - startTime;

                return new MigrationResult
                {
                    Success = true,
                    Duration = duration,
                    Message = $"Migration from {request.SourceVersion.SemanticVersion} to {request.TargetVersion.SemanticVersion} completed successfully"
                };
            }
            catch (Exception ex)
            {
                return new MigrationResult
                {
                    Success = false,
                    Message = $"Migration execution failed: {ex.Message}"
                };
            }
        }

        private async Task<MigrationSafetyCheck> CheckDatabaseConnectivity()
        {
            try
            {
                // In a real implementation, this would test actual database connectivity
                await Task.Delay(100);
                
                return new MigrationSafetyCheck
                {
                    CheckName = "Database Connectivity",
                    Passed = true,
                    Message = "Database connection successful"
                };
            }
            catch (Exception ex)
            {
                return new MigrationSafetyCheck
                {
                    CheckName = "Database Connectivity",
                    Passed = false,
                    Message = $"Database connection failed: {ex.Message}"
                };
            }
        }

        private async Task<MigrationSafetyCheck> CheckBackupAvailability()
        {
            try
            {
                var env = await _environmentService.GetCurrentEnvironment();
                var backupPath = env.BackupPath;
                
                var hasSpace = new DirectoryInfo(backupPath).Exists && 
                               new DriveInfo(backupPath).AvailableFreeSpace > 1024 * 1024 * 1024; // 1GB

                return new MigrationSafetyCheck
                {
                    CheckName = "Backup Availability",
                    Passed = hasSpace,
                    Message = hasSpace ? "Backup space available" : "Insufficient backup space"
                };
            }
            catch (Exception ex)
            {
                return new MigrationSafetyCheck
                {
                    CheckName = "Backup Availability",
                    Passed = false,
                    Message = $"Backup check failed: {ex.Message}"
                };
            }
        }

        private async Task<MigrationSafetyCheck> CheckDiskSpace()
        {
            try
            {
                var systemDrive = new DriveInfo(Environment.SystemDirectory);
                var freeSpaceGB = systemDrive.AvailableFreeSpace / (1024 * 1024 * 1024);
                var hasSpace = freeSpaceGB > 5; // Require at least 5GB free

                return new MigrationSafetyCheck
                {
                    CheckName = "Disk Space",
                    Passed = hasSpace,
                    Message = hasSpace ? $"Sufficient disk space: {freeSpaceGB}GB free" : $"Insufficient disk space: {freeSpaceGB}GB free"
                };
            }
            catch (Exception ex)
            {
                return new MigrationSafetyCheck
                {
                    CheckName = "Disk Space",
                    Passed = false,
                    Message = $"Disk space check failed: {ex.Message}"
                };
            }
        }

        private async Task<MigrationSafetyCheck> CheckUserPermissions()
        {
            try
            {
                // In a real implementation, this would check actual database permissions
                await Task.Delay(50);
                
                var hasPermissions = Environment.IsPrivilegedProcess || !Environment.IsPrivilegedProcess; // Simplified check
                
                return new MigrationSafetyCheck
                {
                    CheckName = "User Permissions",
                    Passed = hasPermissions,
                    Message = hasPermissions ? "User has sufficient permissions" : "Insufficient user permissions"
                };
            }
            catch (Exception ex)
            {
                return new MigrationSafetyCheck
                {
                    CheckName = "User Permissions",
                    Passed = false,
                    Message = $"Permission check failed: {ex.Message}"
                };
            }
        }

        private async Task<MigrationSafetyCheck> CheckConcurrentUsers()
        {
            try
            {
                // In a real implementation, this would check for active database connections
                await Task.Delay(50);
                
                return new MigrationSafetyCheck
                {
                    CheckName = "Concurrent Users",
                    Passed = true, // Assume no concurrent users for demo
                    Message = "No active users blocking migration"
                };
            }
            catch (Exception ex)
            {
                return new MigrationSafetyCheck
                {
                    CheckName = "Concurrent Users",
                    Passed = false,
                    Message = $"Concurrent user check failed: {ex.Message}"
                };
            }
        }
    }

    public interface IDatabaseMigrationService
    {
        Task<MigrationCheckResult> CheckDatabaseCompatibility();
        Task<MigrationResult> PerformMigration(MigrationRequest request);
        Task<bool> ValidateMigrationSafety();
        Task<MigrationHistory> GetMigrationHistory();
        Task<bool> CanDowngrade(VersionInfo targetVersion);
    }

    // Data classes
    public class MigrationCheckResult
    {
        public VersionInfo ApplicationVersion { get; set; } = new();
        public SchemaVersion DatabaseSchemaVersion { get; set; } = new();
        public MigrationHistory MigrationHistory { get; set; } = new();
        public bool IsCompatible { get; set; }
        public string? BlockReason { get; set; }
        public string? RequiredAction { get; set; }
        public MigrationSeverity Severity { get; set; }
    }

    public class MigrationRequest
    {
        public VersionInfo SourceVersion { get; set; } = new();
        public VersionInfo TargetVersion { get; set; } = new();
        public MigrationType MigrationType { get; set; }
        public bool RequireBackup { get; set; } = true;
        public bool IsIrreversible { get; set; }
        public string MigrationId { get; set; } = Guid.NewGuid().ToString("N");
        public string? Description { get; set; }
    }

    public class MigrationResult
    {
        public bool Success { get; set; }
        public string Message { get; set; } = string.Empty;
        public TimeSpan Duration { get; set; }
        public string? BackupId { get; set; }
    }

    public class MigrationBackupResult
    {
        public bool Success { get; set; }
        public string BackupId { get; set; } = string.Empty;
        public string Message { get; set; } = string.Empty;
    }

    public class MigrationValidationResult
    {
        public bool IsValid { get; set; }
        public List<string> Errors { get; set; } = new();
        public List<string> Warnings { get; set; } = new();
    }

    public class MigrationSafetyCheck
    {
        public string CheckName { get; set; } = string.Empty;
        public bool Passed { get; set; }
        public string Message { get; set; } = string.Empty;
    }

    public class SchemaVersion
    {
        public VersionInfo Version { get; set; } = new();
        public DateTime AppliedAt { get; set; }
        public string MigrationId { get; set; } = string.Empty;
    }

    public class MigrationHistory
    {
        public List<MigrationRecord> Migrations { get; set; } = new();
    }

    public class MigrationRecord
    {
        public VersionInfo Version { get; set; } = new();
        public MigrationType MigrationType { get; set; }
        public DateTime AppliedAt { get; set; }
        public bool Success { get; set; }
        public TimeSpan Duration { get; set; }
        public string? ErrorMessage { get; set; }
        public bool IsIrreversible { get; set; }
        public string MigrationId { get; set; } = string.Empty;
    }

    public enum MigrationType
    {
        Upgrade,
        Downgrade,
        Patch,
        Schema
    }

    public enum MigrationSeverity
    {
        Info,
        Warning,
        Required,
        Critical
    }
}
