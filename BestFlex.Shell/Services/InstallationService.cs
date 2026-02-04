using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using System.Windows;
using Microsoft.Extensions.Logging;
using BestFlex.Application.Abstractions;

namespace BestFlex.Shell.Services
{
    /// <summary>
    /// Provides installation detection and first-run experience
    /// </summary>
    public class InstallationService : IInstallationService
    {
        private readonly ILogger<InstallationService> _logger;
        private readonly IStructuredLoggingService _structuredLogger;
        private readonly ICorrelationService _correlationService;
        private readonly IVersioningService _versioningService;
        private readonly IEnvironmentService _environmentService;
        private readonly IDatabaseMigrationService _migrationService;

        private const string InstallationConfigFile = "installation_config.json";
        private const string FirstRunFlagFile = "first_run.flag";

        public InstallationService(
            ILogger<InstallationService> logger,
            IStructuredLoggingService structuredLogger,
            ICorrelationService correlationService,
            IVersioningService versioningService,
            IEnvironmentService environmentService,
            IDatabaseMigrationService migrationService)
        {
            _logger = logger;
            _structuredLogger = structuredLogger;
            _correlationService = correlationService;
            _versioningService = versioningService;
            _environmentService = environmentService;
            _migrationService = migrationService;
        }

        public async Task<InstallationStatus> GetInstallationStatus()
        {
            try
            {
                var status = new InstallationStatus
                {
                    IsFirstRun = await IsFirstRun(),
                    IsInstalled = await IsInstalled(),
                    InstallationDate = await GetInstallationDate(),
                    CurrentVersion = _versioningService.GetCurrentVersion(),
                    Environment = await _environmentService.GetCurrentEnvironment()
                };

                if (status.IsInstalled)
                {
                    status.InstallationConfig = await LoadInstallationConfig();
                    status.LastRunTime = await GetLastRunTime();
                    status.RunCount = await GetRunCount();
                }

                return status;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[INSTALLATION_STATUS_ERROR] Error getting installation status");
                
                return new InstallationStatus
                {
                    IsFirstRun = true,
                    IsInstalled = false,
                    ErrorMessage = $"Error checking installation status: {ex.Message}"
                };
            }
        }

        public async Task<InstallationValidationResult> ValidateInstallation()
        {
            try
            {
                var result = new InstallationValidationResult
                {
                    IsValid = true,
                    CurrentVersion = _versioningService.GetCurrentVersion(),
                    Environment = await _environmentService.GetCurrentEnvironment()
                };

                // Validate environment
                var envValidation = await _environmentService.ValidateEnvironment();
                if (!envValidation)
                {
                    result.IsValid = false;
                    result.Errors.Add("Environment validation failed");
                }

                // Validate database connectivity
                var dbValidation = await ValidateDatabaseConnectivity();
                if (!dbValidation.IsValid)
                {
                    result.IsValid = false;
                    result.Errors.AddRange(dbValidation.Errors);
                }

                // Validate required directories
                var dirValidation = await ValidateRequiredDirectories();
                if (!dirValidation.IsValid)
                {
                    result.IsValid = false;
                    result.Errors.AddRange(dirValidation.Errors);
                }

                // Validate permissions
                var permValidation = await ValidatePermissions();
                if (!permValidation.IsValid)
                {
                    result.IsValid = false;
                    result.Errors.AddRange(permValidation.Errors);
                }

                // Validate configuration
                var configValidation = await ValidateConfiguration();
                if (!configValidation.IsValid)
                {
                    result.IsValid = false;
                    result.Errors.AddRange(configValidation.Errors);
                }

                _structuredLogger.LogSystemEvent("InstallationValidation", 
                    result.IsValid ? "Success" : "Failed", 
                    result.IsValid ? "Installation validated successfully" : string.Join("; ", result.Errors));

                return result;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[INSTALLATION_VALIDATION_ERROR] Error validating installation");
                
                return new InstallationValidationResult
                {
                    IsValid = false,
                    Errors = new List<string> { $"Validation error: {ex.Message}" }
                };
            }
        }

        public async Task<bool> CompleteFirstRun()
        {
            try
            {
                var validation = await ValidateInstallation();
                if (!validation.IsValid)
                {
                    _logger.LogError("[FIRST_RUN_FAILED] Installation validation failed: {Errors}", 
                        string.Join("; ", validation.Errors));
                    return false;
                }

                // Create installation config
                var config = new InstallationConfig
                {
                    InstallationDate = DateTime.UtcNow,
                    Version = _versioningService.GetCurrentVersion(),
                    Environment = await _environmentService.GetCurrentEnvironment(),
                    MachineName = Environment.MachineName,
                    UserName = Environment.UserName,
                    InstallationId = Guid.NewGuid().ToString("N")
                };

                await SaveInstallationConfig(config);

                // Remove first run flag
                if (File.Exists(FirstRunFlagFile))
                {
                    File.Delete(FirstRunFlagFile);
                }

                // Log first run completion
                _structuredLogger.LogUserAction("FirstRunCompleted", "Installation", new Dictionary<string, object>
                {
                    ["Version"] = config.Version.SemanticVersion,
                    ["Environment"] = config.Environment.Name,
                    ["InstallationId"] = config.InstallationId
                });

                _logger.LogInformation("[FIRST_RUN_COMPLETED] Installation completed successfully for version {Version} in {Environment}", 
                    config.Version.SemanticVersion, config.Environment.Name);

                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[FIRST_RUN_ERROR] Error completing first run");
                return false;
            }
        }

        public async Task<FatalErrorResult> CheckForFatalErrors()
        {
            try
            {
                var result = new FatalErrorResult
                {
                    HasFatalErrors = false
                };

                // Check for missing dependencies
                var depCheck = await CheckDependencies();
                if (!depCheck.IsValid)
                {
                    result.HasFatalErrors = true;
                    result.FatalErrors.AddRange(depCheck.Errors);
                }

                // Check database compatibility
                var dbCheck = await _migrationService.CheckDatabaseCompatibility();
                if (!dbCheck.IsCompatible)
                {
                    result.HasFatalErrors = true;
                    result.FatalErrors.Add($"Database compatibility issue: {dbCheck.BlockReason}");
                }

                // Check environment compatibility
                var envCheck = await CheckEnvironmentCompatibility();
                if (!envCheck.IsValid)
                {
                    result.HasFatalErrors = true;
                    result.FatalErrors.AddRange(envCheck.Errors);
                }

                // Check version integrity
                var versionCheck = await _versioningService.ValidateVersionIntegrity();
                if (!versionCheck.IsValid)
                {
                    result.HasFatalErrors = true;
                    result.FatalErrors.AddRange(versionCheck.Errors);
                }

                return result;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[FATAL_ERROR_CHECK_ERROR] Error checking for fatal errors");
                
                return new FatalErrorResult
                {
                    HasFatalErrors = true,
                    FatalErrors = new List<string> { $"Error checking system: {ex.Message}" }
                };
            }
        }

        public async Task ShowFatalErrorDialog(FatalErrorResult errors)
        {
            try
            {
                var message = BuildFatalErrorMessage(errors);
                
                await System.Windows.Application.Current.Dispatcher.InvokeAsync(() =>
                {
                    var result = MessageBox.Show(
                        message,
                        "BestFlex ERP - Fatal Error",
                        MessageBoxButton.OK,
                        MessageBoxImage.Error);

                    _structuredLogger.LogUserAction("FatalErrorShown", "System", new Dictionary<string, object>
                    {
                        ["ErrorCount"] = errors.FatalErrors.Count,
                        ["UserAction"] = result.ToString()
                    });
                });

                // Exit application after fatal error
                Environment.Exit(1);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[FATAL_ERROR_DIALOG_ERROR] Error showing fatal error dialog");
                Environment.Exit(1);
            }
        }

        public async Task<bool> IsUpgradeRequired()
        {
            try
            {
                var currentVersion = _versioningService.GetCurrentVersion();
                var installedVersion = await GetInstalledVersion();

                if (installedVersion == null)
                    return false;

                return currentVersion.Major > installedVersion.Major ||
                       (currentVersion.Major == installedVersion.Major && currentVersion.Minor > installedVersion.Minor);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[UPGRADE_CHECK_ERROR] Error checking if upgrade is required");
                return false;
            }
        }

        private async Task<bool> IsFirstRun()
        {
            return !await IsInstalled() || File.Exists(FirstRunFlagFile);
        }

        private async Task<bool> IsInstalled()
        {
            return File.Exists(InstallationConfigFile);
        }

        private async Task<DateTime?> GetInstallationDate()
        {
            var config = await LoadInstallationConfig();
            return config?.InstallationDate;
        }

        private async Task<InstallationConfig?> LoadInstallationConfig()
        {
            try
            {
                if (!File.Exists(InstallationConfigFile))
                    return null;

                var json = await File.ReadAllTextAsync(InstallationConfigFile);
                return JsonSerializer.Deserialize<InstallationConfig>(json);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "[INSTALLATION_CONFIG_ERROR] Error loading installation config");
                return null;
            }
        }

        private async Task SaveInstallationConfig(InstallationConfig config)
        {
            var json = JsonSerializer.Serialize(config, new JsonSerializerOptions { WriteIndented = true });
            await File.WriteAllTextAsync(InstallationConfigFile, json);
        }

        private async Task<DateTime?> GetLastRunTime()
        {
            try
            {
                var config = await LoadInstallationConfig();
                return config?.LastRunTime;
            }
            catch
            {
                return null;
            }
        }

        private async Task<int> GetRunCount()
        {
            try
            {
                var config = await LoadInstallationConfig();
                return config?.RunCount ?? 0;
            }
            catch
            {
                return 0;
            }
        }

        private async Task<InstallationValidationResult> ValidateDatabaseConnectivity()
        {
            var result = new InstallationValidationResult { IsValid = true };

            try
            {
                // In a real implementation, this would test actual database connectivity
                await Task.Delay(100);
                
                // Simulate connectivity check
                var env = await _environmentService.GetCurrentEnvironment();
                if (string.IsNullOrWhiteSpace(env.DatabaseConnectionString))
                {
                    result.IsValid = false;
                    result.Errors.Add("Database connection string is not configured");
                }
            }
            catch (Exception ex)
            {
                result.IsValid = false;
                result.Errors.Add($"Database connectivity check failed: {ex.Message}");
            }

            return result;
        }

        private async Task<InstallationValidationResult> ValidateRequiredDirectories()
        {
            var result = new InstallationValidationResult { IsValid = true };
            var env = await _environmentService.GetCurrentEnvironment();
            var requiredDirs = new[] { env.LogPath, env.BackupPath };

            foreach (var dir in requiredDirs)
            {
                try
                {
                    if (!Directory.Exists(dir))
                    {
                        Directory.CreateDirectory(dir);
                        result.Warnings.Add($"Created required directory: {dir}");
                    }
                }
                catch (Exception ex)
                {
                    result.IsValid = false;
                    result.Errors.Add($"Cannot create directory {dir}: {ex.Message}");
                }
            }

            return result;
        }

        private async Task<InstallationValidationResult> ValidatePermissions()
        {
            var result = new InstallationValidationResult { IsValid = true };

            try
            {
                // Check write permissions in current directory
                var testFile = Path.Combine(".", "permission_test.tmp");
                await File.WriteAllTextAsync(testFile, "test");
                File.Delete(testFile);
            }
            catch (Exception ex)
            {
                result.IsValid = false;
                result.Errors.Add($"Insufficient permissions: {ex.Message}");
            }

            return result;
        }

        private async Task<InstallationValidationResult> ValidateConfiguration()
        {
            var result = new InstallationValidationResult { IsValid = true };

            try
            {
                var versionValidation = await _versioningService.ValidateVersionIntegrity();
                if (!versionValidation.IsValid)
                {
                    result.IsValid = false;
                    result.Errors.AddRange(versionValidation.Errors);
                }
            }
            catch (Exception ex)
            {
                result.IsValid = false;
                result.Errors.Add($"Configuration validation failed: {ex.Message}");
            }

            return result;
        }

        private async Task<InstallationValidationResult> CheckDependencies()
        {
            var result = new InstallationValidationResult { IsValid = true };

            // In a real implementation, this would check for required dependencies
            // For now, simulate dependency check
            await Task.Delay(50);

            return result;
        }

        private async Task<InstallationValidationResult> CheckEnvironmentCompatibility()
        {
            var result = new InstallationValidationResult { IsValid = true };

            try
            {
                var env = await _environmentService.GetCurrentEnvironment();
                
                if (env.Type == EnvironmentType.Production && !env.RequireHttps)
                {
                    result.IsValid = false;
                    result.Errors.Add("Production environment must require HTTPS");
                }
            }
            catch (Exception ex)
            {
                result.IsValid = false;
                result.Errors.Add($"Environment compatibility check failed: {ex.Message}");
            }

            return result;
        }

        private async Task<VersionInfo?> GetInstalledVersion()
        {
            var config = await LoadInstallationConfig();
            return config?.Version;
        }

        private string BuildFatalErrorMessage(FatalErrorResult errors)
        {
            var message = "BestFlex ERP has encountered fatal errors and cannot continue:\n\n";
            
            message += "Errors:\n";
            foreach (var error in errors.FatalErrors)
            {
                message += $"• {error}\n";
            }

            message += "\nPlease resolve these issues before running the application.\n\n";
            message += "For support, please contact your system administrator.";

            return message;
        }
    }

    public interface IInstallationService
    {
        Task<InstallationStatus> GetInstallationStatus();
        Task<InstallationValidationResult> ValidateInstallation();
        Task<bool> CompleteFirstRun();
        Task<FatalErrorResult> CheckForFatalErrors();
        Task ShowFatalErrorDialog(FatalErrorResult errors);
        Task<bool> IsUpgradeRequired();
    }

    // Data classes
    public class InstallationStatus
    {
        public bool IsFirstRun { get; set; }
        public bool IsInstalled { get; set; }
        public DateTime? InstallationDate { get; set; }
        public VersionInfo CurrentVersion { get; set; } = new();
        public EnvironmentConfig Environment { get; set; } = new();
        public InstallationConfig? InstallationConfig { get; set; }
        public DateTime? LastRunTime { get; set; }
        public int RunCount { get; set; }
        public string? ErrorMessage { get; set; }
    }

    public class InstallationConfig
    {
        public DateTime InstallationDate { get; set; }
        public VersionInfo Version { get; set; } = new();
        public EnvironmentConfig Environment { get; set; } = new();
        public string MachineName { get; set; } = string.Empty;
        public string UserName { get; set; } = string.Empty;
        public string InstallationId { get; set; } = string.Empty;
        public DateTime? LastRunTime { get; set; }
        public int RunCount { get; set; }
    }

    public class InstallationValidationResult
    {
        public bool IsValid { get; set; }
        public VersionInfo CurrentVersion { get; set; } = new();
        public EnvironmentConfig Environment { get; set; } = new();
        public List<string> Errors { get; set; } = new();
        public List<string> Warnings { get; set; } = new();
    }

    public class FatalErrorResult
    {
        public bool HasFatalErrors { get; set; }
        public List<string> FatalErrors { get; set; } = new();
        public string? ErrorMessage { get; set; }
    }
}
