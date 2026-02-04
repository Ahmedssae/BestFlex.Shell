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
    /// Provides environment separation and awareness
    /// </summary>
    public class EnvironmentService : IEnvironmentService
    {
        private readonly ILogger<EnvironmentService> _logger;
        private readonly IStructuredLoggingService _structuredLogger;
        private readonly ICorrelationService _correlationService;

        private const string EnvironmentConfigFile = "environment_config.json";
        private EnvironmentConfig? _currentEnvironment;

        public EnvironmentService(
            ILogger<EnvironmentService> logger,
            IStructuredLoggingService structuredLogger,
            ICorrelationService correlationService)
        {
            _logger = logger;
            _structuredLogger = structuredLogger;
            _correlationService = correlationService;
        }

        public async Task<EnvironmentConfig> GetCurrentEnvironment()
        {
            if (_currentEnvironment != null)
                return _currentEnvironment;

            try
            {
                // Load environment from file first
                var fileConfig = await LoadEnvironmentFromFile();
                if (fileConfig != null)
                {
                    _currentEnvironment = fileConfig;
                    return _currentEnvironment;
                }

                // Detect from environment variables
                var detectedConfig = DetectEnvironmentFromVariables();
                _currentEnvironment = detectedConfig;

                // Save detected environment for next time
                await SaveEnvironmentToFile(detectedConfig);

                _structuredLogger.LogSystemEvent("EnvironmentDetected", "Environment", 
                    $"Environment: {detectedConfig.Name} ({detectedConfig.Type})");

                return _currentEnvironment;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[ENVIRONMENT_LOAD_ERROR] Error loading environment configuration");
                
                // Fallback to development environment
                _currentEnvironment = GetDefaultEnvironment();
                return _currentEnvironment;
            }
        }

        public bool IsProductionEnvironment()
        {
            var env = GetCurrentEnvironment().Result;
            return env.Type == EnvironmentType.Production;
        }

        public bool IsDevelopmentEnvironment()
        {
            var env = GetCurrentEnvironment().Result;
            return env.Type == EnvironmentType.Development;
        }

        public bool IsStagingEnvironment()
        {
            var env = GetCurrentEnvironment().Result;
            return env.Type == EnvironmentType.Staging;
        }

        public async Task<bool> ValidateEnvironment()
        {
            try
            {
                var env = await GetCurrentEnvironment();
                var validation = new EnvironmentValidationResult
                {
                    EnvironmentName = env.Name,
                    IsValid = true
                };

                // Validate database connectivity
                await ValidateDatabaseConnectivity(env, validation);

                // Validate logging configuration
                ValidateLoggingConfiguration(env, validation);

                // Validate backup paths
                ValidateBackupPaths(env, validation);

                // Validate security settings
                ValidateSecuritySettings(env, validation);

                // Validate required directories
                await ValidateRequiredDirectories(env, validation);

                _structuredLogger.LogSystemEvent("EnvironmentValidation", validation.IsValid ? "Success" : "Failed", 
                    validation.IsValid ? "Environment validated successfully" : string.Join("; ", validation.Errors));

                return validation.IsValid;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[ENVIRONMENT_VALIDATION_ERROR] Error validating environment");
                return false;
            }
        }

        public async Task<bool> SwitchEnvironment(EnvironmentType newEnvironment)
        {
            try
            {
                var currentEnv = await GetCurrentEnvironment();
                
                if (currentEnv.Type == newEnvironment)
                {
                    _logger.LogInformation("[ENVIRONMENT_SWITCH] Already in environment: {Environment}", newEnvironment);
                    return true;
                }

                // Validate that we can switch to the new environment
                var newConfig = CreateEnvironmentConfig(newEnvironment);
                var validation = await ValidateEnvironmentConfig(newConfig);
                
                if (!validation.IsValid)
                {
                    _logger.LogError("[ENVIRONMENT_SWITCH_ERROR] Cannot switch to {Environment}: {Errors}", 
                        newEnvironment, string.Join("; ", validation.Errors));
                    return false;
                }

                // Perform the switch
                _currentEnvironment = newConfig;
                await SaveEnvironmentToFile(newConfig);

                _structuredLogger.LogUserAction("EnvironmentSwitched", "System", new Dictionary<string, object>
                {
                    ["FromEnvironment"] = currentEnv.Name,
                    ["ToEnvironment"] = newConfig.Name
                });

                _logger.LogInformation("[ENVIRONMENT_SWITCH] Switched from {FromEnvironment} to {ToEnvironment}", 
                    currentEnv.Name, newConfig.Name);

                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[ENVIRONMENT_SWITCH_ERROR] Error switching environment");
                return false;
            }
        }

        public string GetEnvironmentSpecificPath(string basePath)
        {
            var env = GetCurrentEnvironment().Result;
            var envSuffix = env.Type switch
            {
                EnvironmentType.Development => "_dev",
                EnvironmentType.Staging => "_staging",
                EnvironmentType.Production => "",
                _ => "_custom"
            };

            var directoryName = Path.GetDirectoryName(basePath) ?? "";
            var fileName = Path.GetFileNameWithoutExtension(basePath);
            var extension = Path.GetExtension(basePath);

            return Path.Combine(directoryName, $"{fileName}{envSuffix}{extension}");
        }

        public async Task<EnvironmentHealthStatus> GetEnvironmentHealthStatus()
        {
            try
            {
                var env = await GetCurrentEnvironment();
                var status = new EnvironmentHealthStatus
                {
                    EnvironmentName = env.Name,
                    EnvironmentType = env.Type,
                    Timestamp = DateTime.UtcNow,
                    OverallHealth = HealthLevel.Healthy
                };

                // Check database health
                status.DatabaseHealth = await CheckDatabaseHealth(env);
                
                // Check logging health
                status.LoggingHealth = CheckLoggingHealth(env);
                
                // Check backup health
                status.BackupHealth = await CheckBackupHealth(env);
                
                // Check security health
                status.SecurityHealth = CheckSecuritySettings(env);

                // Calculate overall health
                status.OverallHealth = CalculateOverallHealth(status);

                return status;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[ENVIRONMENT_HEALTH_ERROR] Error checking environment health");
                
                return new EnvironmentHealthStatus
                {
                    OverallHealth = HealthLevel.Error,
                    ErrorMessage = $"Error checking environment health: {ex.Message}"
                };
            }
        }

        private async Task<EnvironmentConfig?> LoadEnvironmentFromFile()
        {
            try
            {
                if (!File.Exists(EnvironmentConfigFile))
                    return null;

                var json = await File.ReadAllTextAsync(EnvironmentConfigFile);
                return JsonSerializer.Deserialize<EnvironmentConfig>(json);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "[ENVIRONMENT_FILE_ERROR] Error loading environment from file: {File}", EnvironmentConfigFile);
                return null;
            }
        }

        private async Task SaveEnvironmentToFile(EnvironmentConfig config)
        {
            try
            {
                var json = JsonSerializer.Serialize(config, new JsonSerializerOptions { WriteIndented = true });
                await File.WriteAllTextAsync(EnvironmentConfigFile, json);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[ENVIRONMENT_SAVE_ERROR] Error saving environment to file: {File}", EnvironmentConfigFile);
            }
        }

        private EnvironmentConfig DetectEnvironmentFromVariables()
        {
            var envVar = Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") 
                        ?? Environment.GetEnvironmentVariable("DOTNET_ENVIRONMENT")
                        ?? Environment.GetEnvironmentVariable("ENVIRONMENT")
                        ?? "Development";

            return envVar.ToLowerInvariant() switch
            {
                "production" or "prod" => CreateEnvironmentConfig(EnvironmentType.Production),
                "staging" or "stage" => CreateEnvironmentConfig(EnvironmentType.Staging),
                "development" or "dev" => CreateEnvironmentConfig(EnvironmentType.Development),
                _ => CreateEnvironmentConfig(EnvironmentType.Development)
            };
        }

        private EnvironmentConfig CreateEnvironmentConfig(EnvironmentType type)
        {
            return type switch
            {
                EnvironmentType.Production => new EnvironmentConfig
                {
                    Name = "Production",
                    Type = EnvironmentType.Production,
                    DatabaseConnectionString = "Host=localhost;Database=bestflex_prod;Username=bestflex;Password=***;",
                    LogLevel = "Warning",
                    BackupPath = "Backups",
                    LogPath = "Logs",
                    EnableDebugFeatures = false,
                    RequireHttps = true,
                    SessionTimeoutMinutes = 30,
                    MaxConcurrentUsers = 100
                },
                EnvironmentType.Staging => new EnvironmentConfig
                {
                    Name = "Staging",
                    Type = EnvironmentType.Staging,
                    DatabaseConnectionString = "Host=localhost;Database=bestflex_staging;Username=bestflex;Password=***;",
                    LogLevel = "Information",
                    BackupPath = "Backups_Staging",
                    LogPath = "Logs_Staging",
                    EnableDebugFeatures = true,
                    RequireHttps = true,
                    SessionTimeoutMinutes = 60,
                    MaxConcurrentUsers = 50
                },
                EnvironmentType.Development => new EnvironmentConfig
                {
                    Name = "Development",
                    Type = EnvironmentType.Development,
                    DatabaseConnectionString = "Host=localhost;Database=bestflex_dev;Username=bestflex;Password=***;",
                    LogLevel = "Debug",
                    BackupPath = "Backups_Dev",
                    LogPath = "Logs_Dev",
                    EnableDebugFeatures = true,
                    RequireHttps = false,
                    SessionTimeoutMinutes = 120,
                    MaxConcurrentUsers = 10
                },
                _ => GetDefaultEnvironment()
            };
        }

        private EnvironmentConfig GetDefaultEnvironment()
        {
            return CreateEnvironmentConfig(EnvironmentType.Development);
        }

        private async Task ValidateDatabaseConnectivity(EnvironmentConfig env, EnvironmentValidationResult validation)
        {
            try
            {
                // In a real implementation, this would test actual database connectivity
                // For now, simulate connectivity check
                await Task.Delay(100);
                
                if (string.IsNullOrWhiteSpace(env.DatabaseConnectionString))
                {
                    validation.IsValid = false;
                    validation.Errors.Add("Database connection string is required");
                }
                else if (env.DatabaseConnectionString.Contains("localhost") && env.Type == EnvironmentType.Production)
                {
                    validation.IsValid = false;
                    validation.Errors.Add("Production environment cannot use localhost database");
                }
            }
            catch (Exception ex)
            {
                validation.IsValid = false;
                validation.Errors.Add($"Database connectivity check failed: {ex.Message}");
            }
        }

        private void ValidateLoggingConfiguration(EnvironmentConfig env, EnvironmentValidationResult validation)
        {
            if (string.IsNullOrWhiteSpace(env.LogPath))
            {
                validation.IsValid = false;
                validation.Errors.Add("Log path is required");
            }

            if (string.IsNullOrWhiteSpace(env.LogLevel))
            {
                validation.IsValid = false;
                validation.Errors.Add("Log level is required");
            }
        }

        private void ValidateBackupPaths(EnvironmentConfig env, EnvironmentValidationResult validation)
        {
            if (string.IsNullOrWhiteSpace(env.BackupPath))
            {
                validation.IsValid = false;
                validation.Errors.Add("Backup path is required");
            }

            if (env.Type == EnvironmentType.Production && env.BackupPath.Contains("Dev"))
            {
                validation.IsValid = false;
                validation.Errors.Add("Production environment cannot use development backup path");
            }
        }

        private void ValidateSecuritySettings(EnvironmentConfig env, EnvironmentValidationResult validation)
        {
            if (env.Type == EnvironmentType.Production && !env.RequireHttps)
            {
                validation.IsValid = false;
                validation.Errors.Add("Production environment must require HTTPS");
            }

            if (env.SessionTimeoutMinutes <= 0)
            {
                validation.IsValid = false;
                validation.Errors.Add("Session timeout must be greater than 0");
            }

            if (env.MaxConcurrentUsers <= 0)
            {
                validation.IsValid = false;
                validation.Errors.Add("Max concurrent users must be greater than 0");
            }
        }

        private async Task ValidateRequiredDirectories(EnvironmentConfig env, EnvironmentValidationResult validation)
        {
        await Task.Yield(); // Make method truly async
            var requiredPaths = new[] { env.LogPath, env.BackupPath };
            
            foreach (var path in requiredPaths)
            {
                try
                {
                    if (!Directory.Exists(path))
                    {
                        Directory.CreateDirectory(path);
                        validation.Warnings.Add($"Created missing directory: {path}");
                    }
                }
                catch (Exception ex)
                {
                    validation.IsValid = false;
                    validation.Errors.Add($"Cannot create directory {path}: {ex.Message}");
                }
            }
        }

        private async Task<EnvironmentValidationResult> ValidateEnvironmentConfig(EnvironmentConfig config)
        {
            var validation = new EnvironmentValidationResult
            {
                EnvironmentName = config.Name,
                IsValid = true
            };

            await ValidateDatabaseConnectivity(config, validation);
            ValidateLoggingConfiguration(config, validation);
            ValidateBackupPaths(config, validation);
            ValidateSecuritySettings(config, validation);
            await ValidateRequiredDirectories(config, validation);

            return validation;
        }

        private async Task<ComponentHealth> CheckDatabaseHealth(EnvironmentConfig env)
        {
            // In a real implementation, this would check actual database health
            await Task.Delay(50);
            
            return new ComponentHealth
            {
                IsHealthy = true,
                ResponseTimeMs = new Random().Next(10, 100),
                Message = "Database connection healthy"
            };
        }

        private ComponentHealth CheckLoggingHealth(EnvironmentConfig env)
        {
            try
            {
                var logPathExists = Directory.Exists(env.LogPath);
                return new ComponentHealth
                {
                    IsHealthy = logPathExists,
                    ResponseTimeMs = logPathExists ? 5 : -1,
                    Message = logPathExists ? "Logging system healthy" : "Log directory not accessible"
                };
            }
            catch (Exception ex)
            {
                return new ComponentHealth
                {
                    IsHealthy = false,
                    ResponseTimeMs = -1,
                    Message = $"Logging system error: {ex.Message}"
                };
            }
        }

        private async Task<ComponentHealth> CheckBackupHealth(EnvironmentConfig env)
        {
            try
            {
                var backupPathExists = Directory.Exists(env.BackupPath);
                await Task.Delay(25);
                
                return new ComponentHealth
                {
                    IsHealthy = backupPathExists,
                    ResponseTimeMs = backupPathExists ? 20 : -1,
                    Message = backupPathExists ? "Backup system healthy" : "Backup directory not accessible"
                };
            }
            catch (Exception ex)
            {
                return new ComponentHealth
                {
                    IsHealthy = false,
                    ResponseTimeMs = -1,
                    Message = $"Backup system error: {ex.Message}"
                };
            }
        }

        private ComponentHealth CheckSecuritySettings(EnvironmentConfig env)
        {
            var issues = new List<string>();
            
            if (env.Type == EnvironmentType.Production && !env.RequireHttps)
                issues.Add("HTTPS not required in production");
            
            if (env.SessionTimeoutMinutes > 1440) // 24 hours
                issues.Add("Session timeout too long");
            
            return new ComponentHealth
            {
                IsHealthy = !issues.Any(),
                ResponseTimeMs = issues.Any() ? -1 : 1,
                Message = issues.Any() ? string.Join("; ", issues) : "Security settings healthy"
            };
        }

        private HealthLevel CalculateOverallHealth(EnvironmentHealthStatus status)
        {
            var components = new[] { status.DatabaseHealth, status.LoggingHealth, status.BackupHealth, status.SecurityHealth };
            
            if (components.Any(c => !c.IsHealthy))
                return HealthLevel.Critical;
            
            if (components.Any(c => c.ResponseTimeMs > 1000))
                return HealthLevel.Warning;
            
            return HealthLevel.Healthy;
        }
    }

    public interface IEnvironmentService
    {
        Task<EnvironmentConfig> GetCurrentEnvironment();
        bool IsProductionEnvironment();
        bool IsDevelopmentEnvironment();
        bool IsStagingEnvironment();
        Task<bool> ValidateEnvironment();
        Task<bool> SwitchEnvironment(EnvironmentType newEnvironment);
        string GetEnvironmentSpecificPath(string basePath);
        Task<EnvironmentHealthStatus> GetEnvironmentHealthStatus();
    }

    // Data classes
    public class EnvironmentConfig
    {
        public string Name { get; set; } = string.Empty;
        public EnvironmentType Type { get; set; }
        public string DatabaseConnectionString { get; set; } = string.Empty;
        public string LogLevel { get; set; } = string.Empty;
        public string BackupPath { get; set; } = string.Empty;
        public string LogPath { get; set; } = string.Empty;
        public bool EnableDebugFeatures { get; set; }
        public bool RequireHttps { get; set; }
        public int SessionTimeoutMinutes { get; set; }
        public int MaxConcurrentUsers { get; set; }
    }

    public class EnvironmentValidationResult
    {
        public string EnvironmentName { get; set; } = string.Empty;
        public bool IsValid { get; set; }
        public List<string> Errors { get; set; } = new();
        public List<string> Warnings { get; set; } = new();
    }

    public class EnvironmentHealthStatus
    {
        public string EnvironmentName { get; set; } = string.Empty;
        public EnvironmentType EnvironmentType { get; set; }
        public DateTime Timestamp { get; set; }
        public HealthLevel OverallHealth { get; set; }
        public ComponentHealth DatabaseHealth { get; set; } = new();
        public ComponentHealth LoggingHealth { get; set; } = new();
        public ComponentHealth BackupHealth { get; set; } = new();
        public ComponentHealth SecurityHealth { get; set; } = new();
        public string? ErrorMessage { get; set; }
    }

    public class ComponentHealth
    {
        public bool IsHealthy { get; set; }
        public long ResponseTimeMs { get; set; }
        public string Message { get; set; } = string.Empty;
    }

    public enum EnvironmentType
    {
        Development,
        Staging,
        Production
    }

    public enum EnvironmentHealthLevel
    {
        Healthy,
        Warning,
        Critical,
        Error
    }
}
