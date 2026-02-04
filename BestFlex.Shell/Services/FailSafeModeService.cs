using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Windows;
using Microsoft.Extensions.Logging;
using BestFlex.Application.Abstractions;
using BestFlex.Infrastructure.Services;

namespace BestFlex.Shell.Services
{
    /// <summary>
    /// Provides fail-safe modes for degraded system states
    /// </summary>
    public class FailSafeModeService : IFailSafeModeService
    {
        private readonly ILogger<FailSafeModeService> _logger;
        private readonly ICurrentUserService _currentUserService;
        private readonly IDatabaseResilienceService _databaseResilience;
        private FailSafeMode _currentMode = FailSafeMode.Normal;
        private readonly Dictionary<string, DateTime> _modeHistory = new();
        private DateTime _lastHealthCheck = DateTime.MinValue;

        public event EventHandler<FailSafeModeChangedEventArgs>? ModeChanged;

        public FailSafeMode CurrentMode => _currentMode;

        public FailSafeModeService(
            ILogger<FailSafeModeService> logger,
            ICurrentUserService currentUserService,
            IDatabaseResilienceService databaseResilience)
        {
            _logger = logger;
            _currentUserService = currentUserService;
            _databaseResilience = databaseResilience;
        }

        public async Task<FailSafeMode> CheckSystemHealthAsync()
        {
            var now = DateTime.UtcNow;
            
            // Don't check too frequently (minimum 30 seconds between checks)
            if (now - _lastHealthCheck < TimeSpan.FromSeconds(30))
            {
                return _currentMode;
            }
            
            _lastHealthCheck = now;
            var correlationId = GenerateCorrelationId();
            var username = _currentUserService.Username ?? "<unknown>";

            _logger.LogDebug("[FAILSAFE_CHECK] [CID:{CorrelationId}] [User:{User}] Starting health check", 
                correlationId, username);

            var newMode = await EvaluateSystemHealth(correlationId);
            
            if (newMode != _currentMode)
            {
                var oldMode = _currentMode;
                _currentMode = newMode;
                
                _modeHistory[$"{now:yyyy-MM-dd HH:mm:ss}"] = now;
                
                _logger.LogInformation("[FAILSAFE_MODE_CHANGE] [CID:{CorrelationId}] [User:{User}] Mode changed from {OldMode} to {NewMode}", 
                    correlationId, username, oldMode, newMode);

                OnModeChanged(new FailSafeModeChangedEventArgs
                {
                    OldMode = oldMode,
                    NewMode = newMode,
                    Timestamp = now,
                    CorrelationId = correlationId,
                    Username = username
                });

                // Apply mode-specific changes
                await ApplyFailSafeMode(newMode, correlationId);
            }

            return _currentMode;
        }

        public bool CanPerformOperation(string operation, string? resource = null)
        {
            return _currentMode switch
            {
                FailSafeMode.Normal => true,
                FailSafeMode.Degraded => IsAllowedInDegradedMode(operation),
                FailSafeMode.ReadOnly => IsAllowedInReadOnlyMode(operation),
                FailSafeMode.Emergency => IsAllowedInEmergencyMode(operation),
                _ => false
            };
        }

        public string GetModeDescription()
        {
            return _currentMode switch
            {
                FailSafeMode.Normal => "System is operating normally",
                FailSafeMode.Degraded => "System is running in degraded mode - some features may be unavailable",
                FailSafeMode.ReadOnly => "System is in read-only mode - data modifications are disabled",
                FailSafeMode.Emergency => "System is in emergency mode - only critical functions available",
                _ => "Unknown system state"
            };
        }

        public string GetModeUserMessage()
        {
            return _currentMode switch
            {
                FailSafeMode.Normal => string.Empty,
                FailSafeMode.Degraded => "⚠️ System is running in degraded mode. Some features may be unavailable. Performance may be reduced.",
                FailSafeMode.ReadOnly => "🔒 System is in read-only mode. Data modifications are temporarily disabled.",
                FailSafeMode.Emergency => "🚨 System is in emergency mode. Only critical functions are available.",
                _ => "⚠️ System is in an unknown state. Please contact support."
            };
        }

        public FailSafeModeHistory GetModeHistory()
        {
            return new FailSafeModeHistory
            {
                CurrentMode = _currentMode,
                ModeChanges = _modeHistory.Select(kvp => new ModeChange
                {
                    Timestamp = kvp.Value,
                    Mode = _currentMode // In a real implementation, this would track actual mode changes
                }).OrderByDescending(x => x.Timestamp).ToList(),
                LastCheck = _lastHealthCheck
            };
        }

        public void ForceMode(FailSafeMode mode, string reason)
        {
            var oldMode = _currentMode;
            _currentMode = mode;
            
            var correlationId = GenerateCorrelationId();
            var username = _currentUserService.Username ?? "<unknown>";
            
            _logger.LogWarning("[FAILSAFE_FORCE] [CID:{CorrelationId}] [User:{User}] Mode forced from {OldMode} to {NewMode} - Reason: {Reason}", 
                correlationId, username, oldMode, mode, reason);

            OnModeChanged(new FailSafeModeChangedEventArgs
            {
                OldMode = oldMode,
                NewMode = mode,
                Timestamp = DateTime.UtcNow,
                CorrelationId = correlationId,
                Username = username,
                Reason = reason
            });

            // Apply mode-specific changes
            System.Windows.Application.Current.Dispatcher.InvokeAsync(() => ApplyFailSafeMode(mode, correlationId));
        }

        public async Task<bool> RecoverFromDegradedMode()
        {
            if (_currentMode != FailSafeMode.Degraded)
                return true;

            _logger.LogInformation("[FAILSAFE_RECOVERY] Attempting to recover from degraded mode");
            
            // Check if underlying issues are resolved
            var healthStatus = await _databaseResilience.CheckDatabaseHealthAsync();
            
            if (healthStatus.IsHealthy && healthStatus.ResponseTime.TotalMilliseconds < 2000)
            {
                ForceMode(FailSafeMode.Normal, "System health recovered");
                return true;
            }
            
            return false;
        }

        private async Task<FailSafeMode> EvaluateSystemHealth(string correlationId)
        {
            try
            {
                // Check database health
                var dbHealth = await _databaseResilience.CheckDatabaseHealthAsync();
                
                if (!dbHealth.IsHealthy)
                {
                    _logger.LogWarning("[FAILSAFE_DB_UNHEALTHY] [CID:{CorrelationId}] Database is unhealthy: {Error}", 
                        correlationId, dbHealth.ErrorMessage);
                    return FailSafeMode.Degraded;
                }

                // Check response time
                if (dbHealth.ResponseTime.TotalMilliseconds > 5000)
                {
                    _logger.LogWarning("[FAILSAFE_DB_SLOW] [CID:{CorrelationId}] Database response time is slow: {ResponseTime}ms", 
                        correlationId, dbHealth.ResponseTime.TotalMilliseconds);
                    return FailSafeMode.Degraded;
                }

                // Check for other system health indicators
                if (await CheckOtherSystemIndicators(correlationId))
                {
                    return FailSafeMode.Degraded;
                }

                return FailSafeMode.Normal;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[FAILSAFE_CHECK_ERROR] [CID:{CorrelationId}] Error during health check", correlationId);
                return FailSafeMode.Emergency;
            }
        }

        private async Task<bool> CheckOtherSystemIndicators(string correlationId)
        {
            // Check memory usage
            var memoryUsage = GC.GetTotalMemory(false);
            var memoryMB = memoryUsage / (1024 * 1024);
            
            if (memoryMB > 1000) // More than 1GB
            {
                _logger.LogWarning("[FAILSAFE_HIGH_MEMORY] [CID:{CorrelationId}] High memory usage: {MemoryMB}MB", 
                    correlationId, memoryMB);
                return true;
            }

            // Check for other indicators (disk space, CPU, etc.)
            // In a real implementation, you would check these system metrics
            
            return false;
        }

        private async Task ApplyFailSafeMode(FailSafeMode mode, string correlationId)
        {
            await System.Windows.Application.Current.Dispatcher.InvokeAsync(() =>
            {
                switch (mode)
                {
                    case FailSafeMode.Degraded:
                        ShowDegradedModeBanner();
                        break;
                    case FailSafeMode.ReadOnly:
                        ShowReadOnlyModeBanner();
                        break;
                    case FailSafeMode.Emergency:
                        ShowEmergencyModeBanner();
                        break;
                    case FailSafeMode.Normal:
                        HideAllBanners();
                        break;
                }
            });
        }

        private void ShowDegradedModeBanner()
        {
            // In a real implementation, this would show a banner in the UI
            _logger.LogInformation("[FAILSAFE_BANNER] Showing degraded mode banner");
        }

        private void ShowReadOnlyModeBanner()
        {
            // In a real implementation, this would show a read-only banner
            _logger.LogInformation("[FAILSAFE_BANNER] Showing read-only mode banner");
        }

        private void ShowEmergencyModeBanner()
        {
            // In a real implementation, this would show an emergency banner
            _logger.LogInformation("[FAILSAFE_BANNER] Showing emergency mode banner");
        }

        private void HideAllBanners()
        {
            // In a real implementation, this would hide all banners
            _logger.LogInformation("[FAILSAFE_BANNER] Hiding all banners");
        }

        private bool IsAllowedInDegradedMode(string operation)
        {
            // Allow read operations and critical write operations
            var allowedOperations = new[]
            {
                "Read", "View", "List", "Search", "Export", "Print",
                "Login", "Logout", "ChangePassword", "ViewProfile",
                "ViewInvoice", "ViewCustomer", "ViewProduct",
                "GenerateReport", "AuditLog", "SystemHealth"
            };

            return allowedOperations.Any(op => operation.Contains(op, StringComparison.OrdinalIgnoreCase));
        }

        private bool IsAllowedInReadOnlyMode(string operation)
        {
            // Only allow read operations
            var allowedOperations = new[]
            {
                "Read", "View", "List", "Search", "Export", "Print",
                "Login", "Logout", "ChangePassword", "ViewProfile",
                "ViewInvoice", "ViewCustomer", "ViewProduct",
                "GenerateReport", "AuditLog", "SystemHealth"
            };

            return allowedOperations.Any(op => operation.Contains(op, StringComparison.OrdinalIgnoreCase));
        }

        private bool IsAllowedInEmergencyMode(string operation)
        {
            // Only allow critical operations
            var allowedOperations = new[]
            {
                "Login", "Logout", "SystemHealth", "EmergencyRecovery",
                "ViewAuditLog", "ViewSystemStatus"
            };

            return allowedOperations.Any(op => operation.Contains(op, StringComparison.OrdinalIgnoreCase));
        }

        private string GenerateCorrelationId()
        {
            var guidStr = Guid.NewGuid().ToString("N");
            return $"FAILSAFE-{DateTime.UtcNow:yyyyMMdd-HHmmss}-{guidStr.Substring(0, 8).ToUpper()}";
        }

        private void OnModeChanged(FailSafeModeChangedEventArgs e)
        {
            ModeChanged?.Invoke(this, e);
        }
    }

    public interface IFailSafeModeService
    {
        event EventHandler<FailSafeModeChangedEventArgs>? ModeChanged;
        FailSafeMode CurrentMode { get; }
        Task<FailSafeMode> CheckSystemHealthAsync();
        bool CanPerformOperation(string operation, string? resource = null);
        string GetModeDescription();
        string GetModeUserMessage();
        FailSafeModeHistory GetModeHistory();
        void ForceMode(FailSafeMode mode, string reason);
        Task<bool> RecoverFromDegradedMode();
    }

    public class FailSafeModeChangedEventArgs : EventArgs
    {
        public FailSafeMode OldMode { get; set; }
        public FailSafeMode NewMode { get; set; }
        public DateTime Timestamp { get; set; }
        public string CorrelationId { get; set; } = string.Empty;
        public string Username { get; set; } = string.Empty;
        public string? Reason { get; set; }
    }

    public class FailSafeModeHistory
    {
        public FailSafeMode CurrentMode { get; set; }
        public List<ModeChange> ModeChanges { get; set; } = new();
        public DateTime LastCheck { get; set; }
    }

    public class ModeChange
    {
        public DateTime Timestamp { get; set; }
        public FailSafeMode Mode { get; set; }
    }

    public enum FailSafeMode
    {
        Normal,
        Degraded,
        ReadOnly,
        Emergency
    }
}
