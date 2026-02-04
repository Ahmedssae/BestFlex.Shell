using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using BestFlex.Application.Abstractions;

namespace BestFlex.Shell.Services
{
    /// <summary>
    /// Centralizes configuration discipline with no magic strings and secure handling of secrets
    /// </summary>
    public class ConfigurationService : IConfigurationService
    {
        private readonly ILogger<ConfigurationService> _logger;
        private readonly IStructuredLoggingService _structuredLogger;
        private readonly ICorrelationService _correlationService;
        private readonly IEnvironmentService _environmentService;
        private readonly IConfiguration _configuration;

        private const string ConfigFile = "appsettings.json";
        private const string EnvironmentConfigFile = "appsettings.{0}.json";
        private const string SecretsFile = "secrets.json";

        public ConfigurationService(
            ILogger<ConfigurationService> logger,
            IStructuredLoggingService structuredLogger,
            ICorrelationService correlationService,
            IEnvironmentService environmentService,
            IConfiguration configuration)
        {
            _logger = logger;
            _structuredLogger = structuredLogger;
            _correlationService = correlationService;
            _environmentService = environmentService;
            _configuration = configuration;
        }

        public async Task<ConfigurationStatus> GetConfigurationStatus()
        {
            try
            {
                var status = new ConfigurationStatus
                {
                    HasConfigurationFile = File.Exists(ConfigFile),
                    HasEnvironmentConfig = await HasEnvironmentSpecificConfig(),
                    HasSecretsFile = File.Exists(SecretsFile),
                    Environment = await _environmentService.GetCurrentEnvironment(),
                    LastConfigReload = await GetLastConfigReloadTime(),
                    ConfigurationErrors = await ValidateConfigurationIntegrity()
                };

                // Check for required settings
                status.RequiredSettings = await GetRequiredSettingsStatus();

                // Check for configuration warnings
                status.Warnings = await GetConfigurationWarnings();

                return status;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[CONFIG_STATUS_ERROR] Error getting configuration status");
                
                return new ConfigurationStatus
                {
                    ErrorMessage = $"Error checking configuration: {ex.Message}"
                };
            }
        }

        public async Task<string> GetConfigurationValue(string key, bool includeSensitive = false)
        {
            try
            {
                var value = _configuration[key];
                
                // Check if this is a sensitive setting
                if (!includeSensitive && IsSensitiveSetting(key))
                {
                    return "***REDACTED***";
                }

                return value?.ToString() ?? string.Empty;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[CONFIG_GET_ERROR] Error getting configuration value for key: {Key}", key);
                return string.Empty;
            }
        }

        public async Task<T?> GetConfigurationValue<T>(string key, T? defaultValue = default, bool includeSensitive = false)
        {
            try
            {
                var value = _configuration.GetValue(key, defaultValue);
                
                // Check if this is a sensitive setting
                if (!includeSensitive && IsSensitiveSetting(key))
                {
                    return defaultValue;
                }

                return value;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[CONFIG_GET_TYPED_ERROR] Error getting typed configuration value for key: {Key}", key);
                return defaultValue;
            }
        }

        public async Task<bool> ValidateConfiguration()
        {
            try
            {
                var validation = new ConfigurationValidationResult
                {
                    IsValid = true
                };

                // Validate required settings
                var requiredValidation = await ValidateRequiredSettings();
                if (!requiredValidation.IsValid)
                {
                    validation.IsValid = false;
                    validation.Errors.AddRange(requiredValidation.Errors);
                }

                // Validate environment-specific settings
                var envValidation = await ValidateEnvironmentSettings();
                if (!envValidation.IsValid)
                {
                    validation.IsValid = false;
                    validation.Errors.AddRange(envValidation.Errors);
                }

                // Validate secrets handling
                var secretsValidation = await ValidateSecretsHandling();
                if (!secretsValidation.IsValid)
                {
                    validation.IsValid = false;
                    validation.Errors.AddRange(secretsValidation.Errors);
                }

                // Validate configuration integrity
                var integrityValidation = await ValidateConfigurationIntegrity();
                if (!integrityValidation.Any())
                {
                    validation.IsValid = false;
                    validation.Errors.AddRange(integrityValidation);
                }

                _structuredLogger.LogSystemEvent("ConfigurationValidation", 
                    validation.IsValid ? "Success" : "Failed", 
                    validation.IsValid ? "Configuration validated successfully" : string.Join("; ", validation.Errors));

                return validation.IsValid;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[CONFIG_VALIDATION_ERROR] Error validating configuration");
                return false;
            }
        }

        public async Task<bool> ReloadConfiguration()
        {
            try
            {
                // In a real implementation, this would reload the configuration
                // For now, simulate configuration reload
                await Task.Delay(100);

                // Update last reload time
                var reloadTime = DateTime.UtcNow;
                await SetLastConfigReloadTime(reloadTime);

                _structuredLogger.LogUserAction("ConfigurationReloaded", "System", new Dictionary<string, object>
                {
                    ["ReloadTime"] = reloadTime
                });

                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[CONFIG_RELOAD_ERROR] Error reloading configuration");
                return false;
            }
        }

        public async Task<Dictionary<string, object>> GetAllSettings()
        {
            try
            {
                var settings = new Dictionary<string, object>();
                
                // Get all configuration keys
                var keys = _configuration.AsEnumerable().ToList();
                
                foreach (var kvp in keys)
                {
                    if (!IsSensitiveSetting(kvp.Key))
                    {
                        settings[kvp.Key] = kvp.Value ?? "";
                    }
                    else
                    {
                        settings[kvp.Key] = "***REDACTED***";
                    }
                }

                return settings;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[CONFIG_GET_ALL_ERROR] Error getting all configuration settings");
                return new Dictionary<string, object>();
            }
        }

        public async Task<bool> SetConfigurationValue(string key, string value, bool isSensitive = false)
        {
            try
            {
                if (isSensitive)
                {
                    return await SetSecretValue(key, value);
                }

                // In a real implementation, this would update the configuration
                // For now, simulate setting the value
                _logger.LogInformation("[CONFIG_SET] Setting configuration value: {Key} = {Value}", key, isSensitive ? "***REDACTED***" : value);
                
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[CONFIG_SET_ERROR] Error setting configuration value for key: {Key}", key);
                return false;
            }
        }

        public async Task<ConfigurationBackupResult> CreateConfigurationBackup()
        {
            try
            {
                var backupId = $"config_backup_{DateTime.UtcNow:yyyyMMddHHmmss}";
                var backupPath = Path.Combine("ConfigBackups", $"{backupId}.json");

                // Create backup directory if it doesn't exist
                    var backupDir = Path.GetDirectoryName(backupPath) ?? throw new InvalidOperationException("Invalid backup path");
                if (!Directory.Exists(backupDir))
                {
                    Directory.CreateDirectory(backupDir);
                }

                // Create backup of current configuration
                var currentConfig = await GetAllSettings();
                var backupJson = JsonSerializer.Serialize(currentConfig, new JsonSerializerOptions { WriteIndented = true });
                await File.WriteAllTextAsync(backupPath, backupJson);

                _structuredLogger.LogUserAction("ConfigurationBackupCreated", "System", new Dictionary<string, object>
                {
                    ["BackupId"] = backupId,
                    ["BackupPath"] = backupPath
                });

                return new ConfigurationBackupResult
                {
                    Success = true,
                    BackupId = backupId,
                    BackupPath = backupPath,
                    Message = "Configuration backup created successfully"
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[CONFIG_BACKUP_ERROR] Error creating configuration backup");
                
                return new ConfigurationBackupResult
                {
                    Success = false,
                    Message = $"Failed to create configuration backup: {ex.Message}"
                };
            }
        }

        private async Task<bool> HasEnvironmentSpecificConfig()
        {
            var env = await _environmentService.GetCurrentEnvironment();
            var envConfigFile = $"appsettings.{env.Type.ToString().ToLowerInvariant()}.json";
            return File.Exists(envConfigFile);
        }

        private async Task<DateTime?> GetLastConfigReloadTime()
        {
            // In a real implementation, this would be stored in a file or database
            // For now, return null
            return null;
        }

        private async Task SetLastConfigReloadTime(DateTime reloadTime)
        {
            // In a real implementation, this would store the reload time
            // For now, just log it
            _logger.LogInformation("[CONFIG_RELOAD_TIME] Configuration reloaded at: {Time}", reloadTime);
        }

        private async Task<List<string>> ValidateConfigurationIntegrity()
        {
            var errors = new List<string>();

            try
            {
                // Check main configuration file
                if (File.Exists(ConfigFile))
                {
                    try
                    {
                        var content = await File.ReadAllTextAsync(ConfigFile);
                        JsonDocument.Parse(content); // Validate JSON format
                    }
                    catch (JsonException ex)
                    {
                        errors.Add($"Invalid JSON format in {ConfigFile}: {ex.Message}");
                    }
                }

                // Check environment-specific configuration
                var env = await _environmentService.GetCurrentEnvironment();
                var envConfigFile = $"appsettings.{env.Type.ToString().ToLowerInvariant()}.json";
                if (File.Exists(envConfigFile))
                {
                    try
                    {
                        var content = await File.ReadAllTextAsync(envConfigFile);
                        JsonDocument.Parse(content); // Validate JSON format
                    }
                    catch (JsonException ex)
                    {
                        errors.Add($"Invalid JSON format in {envConfigFile}: {ex.Message}");
                    }
                }

                // Check secrets file
                if (File.Exists(SecretsFile))
                {
                    try
                    {
                        var content = await File.ReadAllTextAsync(SecretsFile);
                        JsonDocument.Parse(content); // Validate JSON format
                    }
                    catch (JsonException ex)
                    {
                        errors.Add($"Invalid JSON format in {SecretsFile}: {ex.Message}");
                    }
                }
            }
            catch (Exception ex)
            {
                errors.Add($"Configuration integrity check failed: {ex.Message}");
            }

            return errors;
        }

        private async Task<ConfigurationValidationResult> ValidateRequiredSettings()
        {
            var result = new ConfigurationValidationResult { IsValid = true };

            try
            {
                // Check database connection string
                var dbConnection = await GetConfigurationValue("ConnectionStrings:DefaultConnection");
                if (string.IsNullOrWhiteSpace(dbConnection))
                {
                    result.IsValid = false;
                    result.Errors.Add("Database connection string is required");
                }

                // Check logging configuration
                var logLevel = await GetConfigurationValue("Logging:LogLevel");
                if (string.IsNullOrWhiteSpace(logLevel))
                {
                    result.IsValid = false;
                    result.Errors.Add("Logging level is required");
                }

                // Check environment-specific required settings
                var env = await _environmentService.GetCurrentEnvironment();
                if (env.Type == EnvironmentType.Production)
                {
                    var requireHttps = await GetConfigurationValue<bool>("RequireHttps", false);
                    if (!requireHttps)
                    {
                        result.IsValid = false;
                        result.Errors.Add("HTTPS is required in production environment");
                    }
                }

                return result;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[REQUIRED_SETTINGS_ERROR] Error validating required settings");
                
                return new ConfigurationValidationResult
                {
                    IsValid = false,
                    Errors = new List<string> { $"Required settings validation failed: {ex.Message}" }
                };
            }
        }

        private async Task<ConfigurationValidationResult> ValidateEnvironmentSettings()
        {
            var result = new ConfigurationValidationResult { IsValid = true };

            try
            {
                var env = await _environmentService.GetCurrentEnvironment();
                
                // Validate environment-specific settings
                switch (env.Type)
                {
                    case EnvironmentType.Production:
                        var prodDb = await GetConfigurationValue("ConnectionStrings:DefaultConnection");
                        if (prodDb.Contains("localhost") || prodDb.Contains("127.0.0.1"))
                        {
                            result.IsValid = false;
                            result.Errors.Add("Production environment cannot use localhost database");
                        }
                        break;

                    case EnvironmentType.Development:
                        // Development can use localhost, no strict validation
                        break;

                    case EnvironmentType.Staging:
                        // Staging should not use production database
                        var stagingDb = await GetConfigurationValue("ConnectionStrings:DefaultConnection");
                        if (stagingDb.Contains("bestflex_prod"))
                        {
                            result.IsValid = false;
                            result.Errors.Add("Staging environment should not use production database");
                        }
                        break;
                }

                return result;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[ENV_SETTINGS_ERROR] Error validating environment settings");
                
                return new ConfigurationValidationResult
                {
                    IsValid = false,
                    Errors = new List<string> { $"Environment settings validation failed: {ex.Message}" }
                };
            }
        }

        private async Task<ConfigurationValidationResult> ValidateSecretsHandling()
        {
            var result = new ConfigurationValidationResult { IsValid = true };

            try
            {
                // Check if secrets file exists and is properly secured
                if (File.Exists(SecretsFile))
                {
                    var fileInfo = new FileInfo(SecretsFile);
                    
                    // Check file permissions (should be restricted)
                    // In a real implementation, this would check actual file permissions
                    if (fileInfo.Exists)
                    {
                        // For now, just ensure the file exists and is readable
                        try
                        {
                            await File.ReadAllTextAsync(SecretsFile);
                        }
                        catch (UnauthorizedAccessException)
                        {
                            result.IsValid = false;
                            result.Errors.Add("Cannot access secrets file - check permissions");
                        }
                    }
                }

                // Validate that no secrets are hardcoded in main config
                var mainConfigContent = await File.ReadAllTextAsync(ConfigFile);
                var suspiciousPatterns = new[]
                {
                    "password", "secret", "key", "token", "credential", "private"
                };

                foreach (var pattern in suspiciousPatterns)
                {
                    if (mainConfigContent.Contains(pattern, StringComparison.OrdinalIgnoreCase))
                    {
                        result.Warnings.Add($"Potential secret found in main configuration file: {pattern}");
                    }
                }

                return result;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[SECRETS_VALIDATION_ERROR] Error validating secrets handling");
                
                return new ConfigurationValidationResult
                {
                    IsValid = false,
                    Errors = new List<string> { $"Secrets validation failed: {ex.Message}" }
                };
            }
        }

        private async Task<Dictionary<string, string>> GetRequiredSettingsStatus()
        {
            var settings = new Dictionary<string, string>();

            try
            {
                settings["Database"] = string.IsNullOrWhiteSpace(await GetConfigurationValue("ConnectionStrings:DefaultConnection")) ? "Missing" : "OK";
                settings["Logging"] = string.IsNullOrWhiteSpace(await GetConfigurationValue("Logging:LogLevel")) ? "Missing" : "OK";
                settings["Environment"] = (await _environmentService.GetCurrentEnvironment()).Name;
                settings["HTTPS"] = (await GetConfigurationValue<bool>("RequireHttps", false)) ? "Required" : "Not Required";
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[REQUIRED_SETTINGS_ERROR] Error getting required settings status");
                settings["Error"] = "Failed to check settings";
            }

            return settings;
        }

        private async Task<List<string>> GetConfigurationWarnings()
        {
            var warnings = new List<string>();

            try
            {
                // Check for development settings in production
                var env = await _environmentService.GetCurrentEnvironment();
                if (env.Type == EnvironmentType.Production)
                {
                    var logLevel = await GetConfigurationValue("Logging:LogLevel");
                    if (logLevel?.Equals("Debug", StringComparison.OrdinalIgnoreCase) == true)
                    {
                        warnings.Add("Debug logging is enabled in production environment");
                    }

                    var detailedErrors = (await GetConfigurationValue<bool>("IncludeScopes", false)) || false;
                    if (detailedErrors)
                    {
                        warnings.Add("Detailed error logging is enabled in production environment");
                    }
                }

                // Check for missing environment-specific configuration
                if (!await HasEnvironmentSpecificConfig())
                {
                    warnings.Add($"No environment-specific configuration found for {env.Type}");
                }

                // Check for missing secrets file in production
                if (env.Type == EnvironmentType.Production && !File.Exists(SecretsFile))
                {
                    warnings.Add("No secrets file found - consider using environment variables for secrets");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[CONFIG_WARNINGS_ERROR] Error getting configuration warnings");
                warnings.Add("Failed to check configuration warnings");
            }

            return warnings;
        }

        private bool IsSensitiveSetting(string key)
        {
            var sensitivePatterns = new[]
            {
                "password", "secret", "key", "token", "credential", "private",
                "connectionstring", "apikey", "jwt", "certificate"
            };

            return sensitivePatterns.Any(pattern => key.Contains(pattern, StringComparison.OrdinalIgnoreCase));
        }

        private async Task<bool> SetSecretValue(string key, string value)
        {
            try
            {
                // In a real implementation, this would update the secrets file securely
                // For now, just log the action
                _logger.LogInformation("[SECRET_SET] Setting secret value for key: {Key}", key);
                
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[SECRET_SET_ERROR] Error setting secret value for key: {Key}", key);
                return false;
            }
        }
    }

    public interface IConfigurationService
    {
        Task<ConfigurationStatus> GetConfigurationStatus();
        Task<string> GetConfigurationValue(string key, bool includeSensitive = false);
        Task<T?> GetConfigurationValue<T>(string key, T? defaultValue = default, bool includeSensitive = false);
        Task<bool> ValidateConfiguration();
        Task<bool> ReloadConfiguration();
        Task<Dictionary<string, object>> GetAllSettings();
        Task<bool> SetConfigurationValue(string key, string value, bool isSensitive = false);
        Task<ConfigurationBackupResult> CreateConfigurationBackup();
    }

    // Data classes
    public class ConfigurationStatus
    {
        public bool HasConfigurationFile { get; set; }
        public bool HasEnvironmentConfig { get; set; }
        public bool HasSecretsFile { get; set; }
        public EnvironmentConfig Environment { get; set; } = new();
        public DateTime? LastConfigReload { get; set; }
        public List<string> ConfigurationErrors { get; set; } = new();
        public Dictionary<string, string> RequiredSettings { get; set; } = new();
        public List<string> Warnings { get; set; } = new();
        public string? ErrorMessage { get; set; }
    }

    public class ConfigurationValidationResult
    {
        public bool IsValid { get; set; }
        public List<string> Errors { get; set; } = new();
        public List<string> Warnings { get; set; } = new();
    }

    public class ConfigurationBackupResult
    {
        public bool Success { get; set; }
        public string BackupId { get; set; } = string.Empty;
        public string BackupPath { get; set; } = string.Empty;
        public string Message { get; set; } = string.Empty;
    }
}
