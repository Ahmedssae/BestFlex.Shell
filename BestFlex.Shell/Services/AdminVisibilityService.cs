using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using BestFlex.Application.Abstractions;

namespace BestFlex.Shell.Services
{
    /// <summary>
    /// Provides internal diagnostics and admin visibility without user-facing noise
    /// </summary>
    public class AdminVisibilityService : IAdminVisibilityService
    {
        private readonly ILogger<AdminVisibilityService> _logger;
        private readonly ICorrelationService _correlationService;
        private readonly ICurrentUserService _currentUserService;
        private readonly IStructuredLoggingService _structuredLogger;
        private readonly ICrashRecoveryService _crashRecoveryService;
        private readonly IBackupRollbackService _backupService;
        private readonly IFailSafeModeService _failSafeModeService;

        public AdminVisibilityService(
            ILogger<AdminVisibilityService> logger,
            ICorrelationService correlationService,
            ICurrentUserService currentUserService,
            IStructuredLoggingService structuredLogger,
            ICrashRecoveryService crashRecoveryService,
            IBackupRollbackService backupService,
            IFailSafeModeService failSafeModeService)
        {
            _logger = logger;
            _correlationService = correlationService;
            _currentUserService = currentUserService;
            _structuredLogger = structuredLogger;
            _crashRecoveryService = crashRecoveryService;
            _backupService = backupService;
            _failSafeModeService = failSafeModeService;
        }

        public async Task<SystemHealthStatus> GetSystemHealthStatus()
        {
            try
            {
                var status = new SystemHealthStatus
                {
                    Timestamp = DateTime.UtcNow,
                    OverallHealth = HealthLevel.Healthy
                };

                // Database connectivity
                status.DatabaseStatus = await GetDatabaseConnectivityStatus();
                
                // Fail-safe mode
                status.FailSafeMode = _failSafeModeService.CurrentMode;
                status.FailSafeModeMessage = status.FailSafeMode switch
                {
                    FailSafeMode.Normal => "System is operating normally",
                    FailSafeMode.Degraded => "System is running in degraded mode - some features may be unavailable",
                    FailSafeMode.ReadOnly => "System is in read-only mode - data modifications are disabled",
                    FailSafeMode.Emergency => "System is in emergency mode - only critical functions available",
                    _ => "Unknown system state"
                };
                
                // Recent errors
                status.RecentErrors = await GetRecentErrors();
                
                // System resources
                status.SystemResources = GetSystemResourceStatus();
                
                // Service health
                status.ServiceHealth = await GetServiceHealthStatus();
                
                // Backup health
                status.BackupHealth = await _backupService.GetBackupHealthStatus();
                
                // Recent crashes
                status.RecentCrashes = await _crashRecoveryService.GetRecentCrashes(3);
                
                // Calculate overall health
                status.OverallHealth = CalculateOverallHealth(status);

                _structuredLogger.LogSystemEvent("SystemHealthCheck", "AdminVisibility", 
                    $"Overall health: {status.OverallHealth}", new Dictionary<string, object>
                    {
                        ["DatabaseStatus"] = status.DatabaseStatus.IsConnected,
                        ["FailSafeMode"] = status.FailSafeMode.ToString(),
                        ["RecentErrorCount"] = status.RecentErrors.Count,
                        ["OverallHealth"] = status.OverallHealth.ToString()
                    });

                return status;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[SYSTEM_HEALTH_ERROR] Error getting system health status");
                
                return new SystemHealthStatus
                {
                    Timestamp = DateTime.UtcNow,
                    OverallHealth = HealthLevel.Error,
                    ErrorMessage = $"Error getting system health: {ex.Message}"
                };
            }
        }

        public async Task<DatabaseConnectivityStatus> GetDatabaseConnectivityStatus()
        {
            var status = new DatabaseConnectivityStatus
            {
                IsConnected = false,
                LastCheckTime = DateTime.UtcNow,
                ResponseTimeMs = -1
            };

            try
            {
                var stopwatch = Stopwatch.StartNew();
                
                // In a real implementation, this would test actual database connectivity
                // For now, simulate database connectivity check
                await Task.Delay(50); // Simulate network latency
                
                stopwatch.Stop();
                
                status.IsConnected = true;
                status.ResponseTimeMs = stopwatch.ElapsedMilliseconds;
                status.DatabaseVersion = "PostgreSQL 14.0";
                status.ConnectionPoolSize = 10;
                status.ActiveConnections = 3;
                
                _structuredLogger.LogPerformanceMetric("DatabaseConnectivity", status.ResponseTimeMs, "ms", 
                    new Dictionary<string, object> { ["Success"] = status.IsConnected });
            }
            catch (Exception ex)
            {
                status.ErrorMessage = ex.Message;
                _structuredLogger.LogError(ex, "DatabaseConnectivity", "HealthCheck", "Database", 
                    new Dictionary<string, object> { ["ResponseTime"] = status.ResponseTimeMs });
            }

            return status;
        }

        public async Task<List<ErrorSummary>> GetRecentErrors(int count = 10)
        {
            // In a real implementation, this would query log files or error database
            // For now, return simulated recent errors
            var errors = new List<ErrorSummary>();
            
            // Simulate some recent errors for demonstration
            var sampleErrors = new[]
            {
                new { Type = "Database", Message = "Connection timeout", Timestamp = DateTime.UtcNow.AddMinutes(-5) },
                new { Type = "Validation", Message = "Invalid customer data", Timestamp = DateTime.UtcNow.AddMinutes(-15) },
                new { Type = "Security", Message = "Failed login attempt", Timestamp = DateTime.UtcNow.AddMinutes(-30) }
            };

            foreach (var error in sampleErrors.Take(count))
            {
                errors.Add(new ErrorSummary
                {
                    ErrorType = error.Type,
                    Message = error.Message,
                    Timestamp = error.Timestamp,
                    Severity = error.Type == "Security" ? ErrorSeverity.High : ErrorSeverity.Medium
                });
            }

            return errors.OrderByDescending(e => e.Timestamp).ToList();
        }

        public SystemResourceStatus GetSystemResourceStatus()
        {
            var process = Process.GetCurrentProcess();
            
            return new SystemResourceStatus
            {
                CpuUsagePercent = GetCpuUsage(),
                MemoryUsageMB = process.WorkingSet64 / 1024 / 1024,
                AvailableMemoryMB = GetAvailableMemoryMB(),
                DiskUsagePercent = GetDiskUsagePercent(),
                ThreadCount = process.Threads.Count,
                HandleCount = process.HandleCount,
                Timestamp = DateTime.UtcNow
            };
        }

        public async Task<ServiceHealthStatus> GetServiceHealthStatus()
        {
            var services = new List<ServiceHealth>();
            
            // Check critical services
            var criticalServices = new[]
            {
                "CorrelationService", "StructuredLoggingService", "CrashRecoveryService",
                "BackupRollbackService", "FailSafeModeService", "SessionReliabilityService"
            };

            foreach (var serviceName in criticalServices)
            {
                services.Add(new ServiceHealth
                {
                    ServiceName = serviceName,
                    IsHealthy = true, // In real implementation, would check actual service health
                    LastCheckTime = DateTime.UtcNow,
                    ResponseTimeMs = new Random().Next(1, 10) // Simulate response time
                });
            }

            return new ServiceHealthStatus
            {
                Services = services,
                OverallHealth = services.All(s => s.IsHealthy) ? HealthLevel.Healthy : HealthLevel.Warning,
                Timestamp = DateTime.UtcNow
            };
        }

        public async Task<DiagnosticReport> GenerateDiagnosticReport()
        {
            try
            {
                var report = new DiagnosticReport
                {
                    ReportId = Guid.NewGuid().ToString("N"),
                    GeneratedAt = DateTime.UtcNow,
                    GeneratedBy = _currentUserService.Username,
                    SystemHealth = await GetSystemHealthStatus(),
                    RecentCrashes = await _crashRecoveryService.GetRecentCrashes(5),
                    BackupStatus = await _backupService.GetBackupHealthStatus(),
                    ConfigurationInfo = GetConfigurationInfo(),
                    EnvironmentInfo = GetEnvironmentInfo()
                };

                // Save diagnostic report
                await SaveDiagnosticReport(report);

                _structuredLogger.LogUserAction("DiagnosticReportGenerated", "Admin", 
                    new Dictionary<string, object> { ["ReportId"] = report.ReportId });

                return report;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[DIAGNOSTIC_REPORT_ERROR] Error generating diagnostic report");
                
                return new DiagnosticReport
                {
                    ReportId = Guid.NewGuid().ToString("N"),
                    GeneratedAt = DateTime.UtcNow,
                    ErrorMessage = $"Error generating report: {ex.Message}"
                };
            }
        }

        public Task<bool> IsAdminUser()
        {
            // In a real implementation, this would check user roles/permissions
            // For now, assume admin if username contains "admin" or user has specific role
            var username = _currentUserService.Username?.ToLowerInvariant() ?? "";
            return Task.FromResult(username.Contains("admin") || username == "system");
        }

        public async Task<List<string>> GetSystemWarnings()
        {
            var warnings = new List<string>();
            
            try
            {
                var health = await GetSystemHealthStatus();
                
                // Check fail-safe mode
                if (health.FailSafeMode != FailSafeMode.Normal)
                {
                    warnings.Add($"System is in {health.FailSafeMode} mode: {health.FailSafeModeMessage}");
                }
                
                // Check database connectivity
                if (!health.DatabaseStatus.IsConnected)
                {
                    warnings.Add("Database connectivity issues detected");
                }
                
                // Check recent errors
                if (health.RecentErrors.Count > 5)
                {
                    warnings.Add($"High error rate: {health.RecentErrors.Count} recent errors");
                }
                
                // Check system resources
                if (health.SystemResources.MemoryUsageMB > 1000)
                {
                    warnings.Add($"High memory usage: {health.SystemResources.MemoryUsageMB}MB");
                }
                
                // Check backup health
                if (health.BackupHealth.HealthLevel == BackupHealthLevel.Critical)
                {
                    warnings.Add("Backup system requires attention");
                }
                
                // Check recent crashes
                if (health.RecentCrashes.Any(c => DateTime.UtcNow - c.CrashTime < TimeSpan.FromHours(1)))
                {
                    warnings.Add("Recent application crash detected");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[SYSTEM_WARNINGS_ERROR] Error getting system warnings");
                warnings.Add("Error checking system warnings");
            }

            return warnings;
        }

        private HealthLevel CalculateOverallHealth(SystemHealthStatus status)
        {
            var healthFactors = new List<HealthLevel>();

            // Database connectivity
            healthFactors.Add(status.DatabaseStatus.IsConnected ? HealthLevel.Healthy : HealthLevel.Critical);

            // Fail-safe mode
            healthFactors.Add(status.FailSafeMode == FailSafeMode.Normal ? HealthLevel.Healthy : 
                             status.FailSafeMode == FailSafeMode.Emergency ? HealthLevel.Critical : HealthLevel.Warning);

            // Recent errors
            healthFactors.Add(status.RecentErrors.Count > 10 ? HealthLevel.Warning : HealthLevel.Healthy);

            // System resources
            healthFactors.Add(status.SystemResources.MemoryUsageMB > 1000 ? HealthLevel.Warning : HealthLevel.Healthy);

            // Backup health
            healthFactors.Add(status.BackupHealth.HealthLevel switch
            {
                BackupHealthLevel.Healthy => HealthLevel.Healthy,
                BackupHealthLevel.Warning => HealthLevel.Warning,
                BackupHealthLevel.Critical or BackupHealthLevel.Error => HealthLevel.Critical,
                _ => HealthLevel.Warning
            });

            // Service health
            healthFactors.Add(status.ServiceHealth.OverallHealth);

            // Overall health is the worst of all factors
            if (healthFactors.Contains(HealthLevel.Critical))
                return HealthLevel.Critical;
            if (healthFactors.Contains(HealthLevel.Warning))
                return HealthLevel.Warning;
            if (healthFactors.Contains(HealthLevel.Error))
                return HealthLevel.Error;

            return HealthLevel.Healthy;
        }

        private double GetCpuUsage()
        {
            // In a real implementation, this would get actual CPU usage
            return new Random().NextDouble() * 100;
        }

        private long GetAvailableMemoryMB()
        {
            // In a real implementation, this would get actual available memory
            var gc = GC.GetTotalMemory(false);
            return Math.Max(0, (Environment.WorkingSet - gc) / 1024 / 1024);
        }

        private double GetDiskUsagePercent()
        {
            try
            {
                var drive = new DriveInfo(Path.GetPathRoot(Environment.CurrentDirectory) ?? "C:");
                return (double)(drive.TotalSize - drive.AvailableFreeSpace) / drive.TotalSize * 100;
            }
            catch
            {
                return 0;
            }
        }

        private ConfigurationInfo GetConfigurationInfo()
        {
            return new ConfigurationInfo
            {
                ApplicationVersion = GetApplicationVersion(),
                ConfigurationPath = "appsettings.json",
                LogLevel = "Information",
                FeaturesEnabled = new[] { "Correlation", "StructuredLogging", "CrashRecovery", "Backup", "FailSafe" }
            };
        }

        private EnvironmentInfo GetEnvironmentInfo()
        {
            return new EnvironmentInfo
            {
                MachineName = Environment.MachineName,
                OSVersion = Environment.OSVersion.ToString(),
                ProcessorCount = Environment.ProcessorCount,
                CLRVersion = Environment.Version.ToString(),
                Is64BitProcess = Environment.Is64BitProcess,
                WorkingDirectory = Environment.CurrentDirectory
            };
        }

        private string GetApplicationVersion()
        {
            try
            {
                var assembly = System.Reflection.Assembly.GetExecutingAssembly();
                return assembly.GetName().Version?.ToString() ?? "Unknown";
            }
            catch
            {
                return "Unknown";
            }
        }

        private async Task SaveDiagnosticReport(DiagnosticReport report)
        {
            try
            {
                var reportsDir = "DiagnosticReports";
                if (!Directory.Exists(reportsDir))
                {
                    Directory.CreateDirectory(reportsDir);
                }

                var fileName = $"diagnostic_{report.ReportId}_{DateTime.Now:yyyyMMddHHmmss}.json";
                var filePath = Path.Combine(reportsDir, fileName);

                var json = JsonSerializer.Serialize(report, new JsonSerializerOptions { WriteIndented = true });
                await File.WriteAllTextAsync(filePath, json);

                _logger.LogInformation("[DIAGNOSTIC_REPORT_SAVED] [RID:{ReportId}] Diagnostic report saved to {Path}", 
                    report.ReportId, filePath);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[DIAGNOSTIC_REPORT_SAVE_ERROR] [RID:{ReportId}] Error saving diagnostic report", 
                    report.ReportId);
            }
        }
    }

    public interface IAdminVisibilityService
    {
        Task<SystemHealthStatus> GetSystemHealthStatus();
        Task<DatabaseConnectivityStatus> GetDatabaseConnectivityStatus();
        Task<List<ErrorSummary>> GetRecentErrors(int count = 10);
        SystemResourceStatus GetSystemResourceStatus();
        Task<ServiceHealthStatus> GetServiceHealthStatus();
        Task<DiagnosticReport> GenerateDiagnosticReport();
        Task<bool> IsAdminUser();
        Task<List<string>> GetSystemWarnings();
    }

    // Data classes for admin visibility
    public class SystemHealthStatus
    {
        public DateTime Timestamp { get; set; }
        public HealthLevel OverallHealth { get; set; }
        public DatabaseConnectivityStatus DatabaseStatus { get; set; } = new();
        public FailSafeMode FailSafeMode { get; set; }
        public string FailSafeModeMessage { get; set; } = string.Empty;
        public List<ErrorSummary> RecentErrors { get; set; } = new();
        public SystemResourceStatus SystemResources { get; set; } = new();
        public ServiceHealthStatus ServiceHealth { get; set; } = new();
        public BackupHealthStatus BackupHealth { get; set; } = new();
        public List<CrashInfo> RecentCrashes { get; set; } = new();
        public string? ErrorMessage { get; set; }
    }

    public class DatabaseConnectivityStatus
    {
        public bool IsConnected { get; set; }
        public DateTime LastCheckTime { get; set; }
        public long ResponseTimeMs { get; set; }
        public string? DatabaseVersion { get; set; }
        public int ConnectionPoolSize { get; set; }
        public int ActiveConnections { get; set; }
        public string? ErrorMessage { get; set; }
    }

    public class ErrorSummary
    {
        public string ErrorType { get; set; } = string.Empty;
        public string Message { get; set; } = string.Empty;
        public DateTime Timestamp { get; set; }
        public ErrorSeverity Severity { get; set; }
    }

    public class SystemResourceStatus
    {
        public double CpuUsagePercent { get; set; }
        public long MemoryUsageMB { get; set; }
        public long AvailableMemoryMB { get; set; }
        public double DiskUsagePercent { get; set; }
        public int ThreadCount { get; set; }
        public int HandleCount { get; set; }
        public DateTime Timestamp { get; set; }
    }

    public class ServiceHealthStatus
    {
        public List<ServiceHealth> Services { get; set; } = new();
        public HealthLevel OverallHealth { get; set; }
        public DateTime Timestamp { get; set; }
    }

    public class ServiceHealth
    {
        public string ServiceName { get; set; } = string.Empty;
        public bool IsHealthy { get; set; }
        public DateTime LastCheckTime { get; set; }
        public long ResponseTimeMs { get; set; }
    }

    public class DiagnosticReport
    {
        public string ReportId { get; set; } = string.Empty;
        public DateTime GeneratedAt { get; set; }
        public string GeneratedBy { get; set; } = string.Empty;
        public SystemHealthStatus SystemHealth { get; set; } = new();
        public List<CrashInfo> RecentCrashes { get; set; } = new();
        public BackupHealthStatus BackupStatus { get; set; } = new();
        public ConfigurationInfo ConfigurationInfo { get; set; } = new();
        public EnvironmentInfo EnvironmentInfo { get; set; } = new();
        public string? ErrorMessage { get; set; }
    }

    public class ConfigurationInfo
    {
        public string ApplicationVersion { get; set; } = string.Empty;
        public string ConfigurationPath { get; set; } = string.Empty;
        public string LogLevel { get; set; } = string.Empty;
        public string[] FeaturesEnabled { get; set; } = Array.Empty<string>();
    }

    public class EnvironmentInfo
    {
        public string MachineName { get; set; } = string.Empty;
        public string OSVersion { get; set; } = string.Empty;
        public int ProcessorCount { get; set; }
        public string CLRVersion { get; set; } = string.Empty;
        public bool Is64BitProcess { get; set; }
        public string WorkingDirectory { get; set; } = string.Empty;
    }

    public enum HealthLevel
    {
        Healthy,
        Warning,
        Critical,
        Error
    }

    public enum ErrorSeverity
    {
        Low,
        Medium,
        High,
        Critical
    }
}
