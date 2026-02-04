using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text.Json;
using System.Threading.Tasks;
using System.Windows;
using Microsoft.Extensions.Logging;
using BestFlex.Application.Abstractions;

namespace BestFlex.Shell.Services
{
    /// <summary>
    /// Handles crash recovery with context capture and safe restart
    /// </summary>
    public class CrashRecoveryService : ICrashRecoveryService
    {
        private readonly ILogger<CrashRecoveryService> _logger;
        private readonly ICorrelationService _correlationService;
        private readonly ICurrentUserService _currentUserService;
        private readonly IStructuredLoggingService _structuredLogger;

        private const string CrashReportDirectory = "CrashReports";
        private const string CrashIndicatorFile = "crash_indicator.json";
        private const int MaxCrashReports = 10;

        public CrashRecoveryService(
            ILogger<CrashRecoveryService> logger,
            ICorrelationService correlationService,
            ICurrentUserService currentUserService,
            IStructuredLoggingService structuredLogger)
        {
            _logger = logger;
            _correlationService = correlationService;
            _currentUserService = currentUserService;
            _structuredLogger = structuredLogger;

            EnsureCrashReportDirectory();
        }

        public async Task<bool> CheckForPreviousCrash()
        {
            try
            {
                var crashIndicatorPath = Path.Combine(CrashReportDirectory, CrashIndicatorFile);
                
                if (!File.Exists(crashIndicatorPath))
                {
                    _logger.LogInformation("[CRASH_CHECK] No previous crash detected");
                    return false;
                }

                var crashData = await File.ReadAllTextAsync(crashIndicatorPath);
                var crashInfo = JsonSerializer.Deserialize<CrashInfo>(crashData);

                if (crashInfo == null)
                {
                    _logger.LogWarning("[CRASH_CHECK] Invalid crash indicator file found");
                    return false;
                }

                // Check if crash was recent (within last 5 minutes)
                if (DateTime.UtcNow - crashInfo.CrashTime < TimeSpan.FromMinutes(5))
                {
                    _logger.LogWarning("[CRASH_DETECTED] Previous crash detected at {CrashTime}", crashInfo.CrashTime);
                    _structuredLogger.LogSystemEvent("CrashDetected", "Application", $"Previous crash at {crashInfo.CrashTime}");
                    return true;
                }
                else
                {
                    // Old crash indicator, clean it up
                    _logger.LogInformation("[CRASH_CLEANUP] Cleaning up old crash indicator from {CrashTime}", crashInfo.CrashTime);
                    File.Delete(crashIndicatorPath);
                    return false;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[CRASH_CHECK_ERROR] Error checking for previous crash");
                return false;
            }
        }

        public async Task<CrashRecoveryResult> HandleCrash(Exception exception, string context = "Unhandled")
        {
            var crashInfo = new CrashInfo
            {
                CrashId = Guid.NewGuid().ToString("N"),
                CrashTime = DateTime.UtcNow,
                ExceptionType = exception.GetType().Name,
                ExceptionMessage = exception.Message,
                StackTrace = exception.StackTrace ?? "",
                Context = context,
                Username = _currentUserService.Username,
                UserId = _currentUserService.UserId,
                OperationId = _correlationService.CurrentContext.OperationId,
                MachineName = Environment.MachineName,
                ProcessId = Process.GetCurrentProcess().Id,
                ApplicationVersion = GetApplicationVersion(),
                SystemInfo = GetSystemInfo()
            };

            try
            {
                // Save crash report
                var crashReportPath = await SaveCrashReport(crashInfo);
                
                // Create crash indicator for next startup
                await CreateCrashIndicator(crashInfo);

                // Log the crash
                _structuredLogger.LogCriticalFailure("ApplicationCrash", 
                    $"Application crashed: {exception.Message}", 
                    context, 
                    new Dictionary<string, object>
                    {
                        ["CrashId"] = crashInfo.CrashId,
                        ["ExceptionType"] = crashInfo.ExceptionType,
                        ["OperationId"] = crashInfo.OperationId
                    });

                _logger.LogError(exception, "[CRASH_HANDLED] [CID:{CrashId}] Crash handled and report saved to {Path}", 
                    crashInfo.CrashId, crashReportPath);

                return new CrashRecoveryResult
                {
                    Success = true,
                    CrashId = crashInfo.CrashId,
                    ReportPath = crashReportPath,
                    Message = "A crash report has been generated. The application will attempt to restart in safe mode."
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[CRASH_HANDLING_FAILED] Failed to handle crash properly");
                
                return new CrashRecoveryResult
                {
                    Success = false,
                    Message = "Failed to generate crash report. Please contact support."
                };
            }
        }

        public async Task<bool> ShowRecoveryDialog()
        {
            try
            {
                var crashIndicatorPath = Path.Combine(CrashReportDirectory, CrashIndicatorFile);
                var crashData = await File.ReadAllTextAsync(crashIndicatorPath);
                var crashInfo = JsonSerializer.Deserialize<CrashInfo>(crashData);

                if (crashInfo == null)
                    return false;

                var result = await System.Windows.Application.Current.Dispatcher.InvokeAsync(() =>
                {
                    var message = $"The application did not close properly on {crashInfo.CrashTime:yyyy-MM-dd HH:mm:ss}.\n\n" +
                                 $"Error: {crashInfo.ExceptionMessage}\n\n" +
                                 $"A crash report has been generated (ID: {crashInfo.CrashId}).\n\n" +
                                 "Would you like to:\n" +
                                 "• Continue in safe mode\n" +
                                 "• Export crash report for support\n" +
                                 "• Restart normally";

                    var choice = MessageBox.Show(message, "Application Recovery", 
                        MessageBoxButton.YesNoCancel, MessageBoxImage.Warning);

                    return choice switch
                    {
                        MessageBoxResult.Yes => true,  // Continue in safe mode
                        MessageBoxResult.No => false,   // Restart normally
                        _ => false                     // Cancel, treat as restart
                    };
                });

                if (result)
                {
                    _structuredLogger.LogUserAction("RecoveryChoice", "SafeMode", new Dictionary<string, object>
                    {
                        ["CrashId"] = crashInfo.CrashId,
                        ["Choice"] = "SafeMode"
                    });
                }
                else
                {
                    _structuredLogger.LogUserAction("RecoveryChoice", "NormalRestart", new Dictionary<string, object>
                    {
                        ["CrashId"] = crashInfo.CrashId,
                        ["Choice"] = "NormalRestart"
                    });
                }

                return result;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[RECOVERY_DIALOG_ERROR] Error showing recovery dialog");
                return false;
            }
        }

        public async Task<string> ExportCrashReport(string crashId)
        {
            try
            {
                var crashReportPath = Path.Combine(CrashReportDirectory, $"crash_{crashId}.json");
                
                if (!File.Exists(crashReportPath))
                {
                    throw new FileNotFoundException($"Crash report not found: {crashId}");
                }

                var exportPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Desktop), $"BestFlex_CrashReport_{crashId}_{DateTime.Now:yyyyMMddHHmmss}.json");
                
                await FileExtensions.CopyAsync(crashReportPath, exportPath);

                _structuredLogger.LogUserAction("CrashReportExported", "File", new Dictionary<string, object>
                {
                    ["CrashId"] = crashId,
                    ["ExportPath"] = exportPath
                });

                _logger.LogInformation("[CRASH_REPORT_EXPORTED] [CID:{CrashId}] Crash report exported to {Path}", crashId, exportPath);

                return exportPath;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[CRASH_EXPORT_ERROR] [CID:{CrashId}] Error exporting crash report", crashId);
                throw;
            }
        }

        public void ClearCrashIndicator()
        {
            try
            {
                var crashIndicatorPath = Path.Combine(CrashReportDirectory, CrashIndicatorFile);
                
                if (File.Exists(crashIndicatorPath))
                {
                    File.Delete(crashIndicatorPath);
                    _logger.LogInformation("[CRASH_INDICATOR_CLEARED] Crash indicator cleared");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[CRASH_INDICATOR_CLEAR_ERROR] Error clearing crash indicator");
            }
        }

        public async Task<List<CrashInfo>> GetRecentCrashes(int count = 5)
        {
            var crashes = new List<CrashInfo>();

            try
            {
                if (!Directory.Exists(CrashReportDirectory))
                    return crashes;

                var crashFiles = Directory.GetFiles(CrashReportDirectory, "crash_*.json")
                    .OrderByDescending(f => File.GetLastWriteTime(f))
                    .Take(count);

                foreach (var file in crashFiles)
                {
                    try
                    {
                        var content = await File.ReadAllTextAsync(file);
                        var crashInfo = JsonSerializer.Deserialize<CrashInfo>(content);
                        if (crashInfo != null)
                        {
                            crashes.Add(crashInfo);
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "[CRASH_READ_ERROR] Error reading crash file: {File}", file);
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[CRASH_LIST_ERROR] Error getting recent crashes");
            }

            return crashes;
        }

        private void EnsureCrashReportDirectory()
        {
            try
            {
                if (!Directory.Exists(CrashReportDirectory))
                {
                    Directory.CreateDirectory(CrashReportDirectory);
                    _logger.LogInformation("[CRASH_DIR_CREATED] Crash report directory created: {Directory}", CrashReportDirectory);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[CRASH_DIR_ERROR] Error creating crash report directory");
            }
        }

        private async Task<string> SaveCrashReport(CrashInfo crashInfo)
        {
            var fileName = $"crash_{crashInfo.CrashId}.json";
            var filePath = Path.Combine(CrashReportDirectory, fileName);

            var json = JsonSerializer.Serialize(crashInfo, new JsonSerializerOptions { WriteIndented = true });
            await File.WriteAllTextAsync(filePath, json);

            // Clean up old crash reports
            await CleanupOldCrashReports();

            return filePath;
        }

        private async Task CreateCrashIndicator(CrashInfo crashInfo)
        {
            var indicatorPath = Path.Combine(CrashReportDirectory, CrashIndicatorFile);
            var json = JsonSerializer.Serialize(crashInfo, new JsonSerializerOptions { WriteIndented = true });
            await File.WriteAllTextAsync(indicatorPath, json);
        }

        private async Task CleanupOldCrashReports()
        {
            try
            {
                if (!Directory.Exists(CrashReportDirectory))
                    return;

                var crashFiles = Directory.GetFiles(CrashReportDirectory, "crash_*.json")
                    .OrderByDescending(f => File.GetLastWriteTime(f))
                    .Skip(MaxCrashReports);

                foreach (var file in crashFiles)
                {
                    try
                    {
                        File.Delete(file);
                        _logger.LogDebug("[CRASH_CLEANUP] Old crash report removed: {File}", file);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "[CRASH_CLEANUP_ERROR] Error removing old crash report: {File}", file);
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[CRASH_CLEANUP_ERROR] Error cleaning up old crash reports");
            }
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

        private Dictionary<string, object> GetSystemInfo()
        {
            return new Dictionary<string, object>
            {
                ["OSVersion"] = Environment.OSVersion.ToString(),
                ["ProcessorCount"] = Environment.ProcessorCount,
                ["WorkingSet"] = Environment.WorkingSet,
                ["Is64BitProcess"] = Environment.Is64BitProcess,
                ["CLRVersion"] = Environment.Version.ToString()
            };
        }
    }

    public interface ICrashRecoveryService
    {
        Task<bool> CheckForPreviousCrash();
        Task<CrashRecoveryResult> HandleCrash(Exception exception, string context = "Unhandled");
        Task<bool> ShowRecoveryDialog();
        Task<string> ExportCrashReport(string crashId);
        void ClearCrashIndicator();
        Task<List<CrashInfo>> GetRecentCrashes(int count = 5);
    }

    public class CrashRecoveryResult
    {
        public bool Success { get; set; }
        public string CrashId { get; set; } = string.Empty;
        public string ReportPath { get; set; } = string.Empty;
        public string Message { get; set; } = string.Empty;
    }

    public class CrashInfo
    {
        public string CrashId { get; set; } = string.Empty;
        public DateTime CrashTime { get; set; }
        public string ExceptionType { get; set; } = string.Empty;
        public string ExceptionMessage { get; set; } = string.Empty;
        public string StackTrace { get; set; } = string.Empty;
        public string Context { get; set; } = string.Empty;
        public string? Username { get; set; }
        public Guid? UserId { get; set; }
        public string OperationId { get; set; } = string.Empty;
        public string MachineName { get; set; } = string.Empty;
        public int ProcessId { get; set; }
        public string ApplicationVersion { get; set; } = string.Empty;
        public Dictionary<string, object> SystemInfo { get; set; } = new();
    }

    /// <summary>
    /// Global exception handler for crash recovery
    /// </summary>
    public class GlobalExceptionHandler
    {
        private readonly ICrashRecoveryService _crashRecoveryService;
        private readonly ILogger<GlobalExceptionHandler> _logger;

        public GlobalExceptionHandler(ICrashRecoveryService crashRecoveryService, ILogger<GlobalExceptionHandler> logger)
        {
            _crashRecoveryService = crashRecoveryService;
            _logger = logger;
        }

        public async Task HandleUnhandledException(Exception exception, string context = "Unhandled")
        {
            await Task.Yield(); // Make method truly async
            _logger.LogError(exception, "[GLOBAL_EXCEPTION] Unhandled exception in context: {Context}", context);
            
            var result = await _crashRecoveryService.HandleCrash(exception, context);
            
            // Show crash report to user
            await System.Windows.Application.Current.Dispatcher.InvokeAsync(() =>
            {
                MessageBox.Show(result.Message, "Application Crash", MessageBoxButton.OK, MessageBoxImage.Error);
            });

            // Exit the application
            Environment.Exit(1);
        }
    }

    /// <summary>
    /// File copy helper for async operations
    /// </summary>
    internal static class FileExtensions
    {
        public static async Task CopyAsync(string sourceFile, string destinationFile)
        {
            using var sourceStream = new FileStream(sourceFile, FileMode.Open, FileAccess.Read, FileShare.Read, bufferSize: 4096, useAsync: true);
            using var destinationStream = new FileStream(destinationFile, FileMode.Create, FileAccess.Write, FileShare.None, bufferSize: 4096, useAsync: true);
            await sourceStream.CopyToAsync(destinationStream);
        }
    }
}
