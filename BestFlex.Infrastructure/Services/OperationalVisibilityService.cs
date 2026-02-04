using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using BestFlex.Application.Abstractions;

namespace BestFlex.Infrastructure.Services
{
    /// <summary>
    /// Provides operational visibility with correlation IDs and performance monitoring
    /// </summary>
    public class OperationalVisibilityService : IOperationalVisibilityService
    {
        private readonly ILogger<OperationalVisibilityService> _logger;
        private readonly ICurrentUserService _currentUserService;
        private readonly ConcurrentDictionary<string, OperationContext> _activeOperations = new();
        private readonly ConcurrentDictionary<string, PerformanceMetrics> _performanceMetrics = new();

        public OperationalVisibilityService(
            ILogger<OperationalVisibilityService> logger,
            ICurrentUserService currentUserService)
        {
            _logger = logger;
            _currentUserService = currentUserService;
        }

        public string GenerateCorrelationId(string operationType = "OP")
        {
            var guidStr = Guid.NewGuid().ToString("N");
            var correlationId = $"{operationType}-{DateTime.UtcNow:yyyyMMdd-HHmmss}-{guidStr.Substring(0, 8).ToUpper()}";
            
            // Track the operation start
            _activeOperations.TryAdd(correlationId, new OperationContext
            {
                CorrelationId = correlationId,
                OperationType = operationType,
                StartTime = DateTime.UtcNow,
                User = _currentUserService.Username ?? "<unknown>"
            });

            return correlationId;
        }

        public void LogOperationStart(string correlationId, string operationName, Dictionary<string, object>? parameters = null)
        {
            var context = _activeOperations.GetOrAdd(correlationId, new OperationContext
            {
                CorrelationId = correlationId,
                OperationType = "UNKNOWN",
                StartTime = DateTime.UtcNow,
                User = _currentUserService.Username ?? "<unknown>"
            });

            context.OperationName = operationName;
            context.Parameters = parameters ?? new Dictionary<string, object>();

            _logger.LogInformation("[OPERATION_START] [CID:{CorrelationId}] [User:{User}] {OperationName} started with {ParamCount} parameters", 
                correlationId, context.User, operationName, context.Parameters.Count);
        }

        public void LogOperationEnd(string correlationId, bool success, string? errorMessage = null)
        {
            if (_activeOperations.TryRemove(correlationId, out var context))
            {
                var duration = DateTime.UtcNow - context.StartTime;
                
                // Log operation completion
                if (success)
                {
                    _logger.LogInformation("[OPERATION_END] [CID:{CorrelationId}] [User:{User}] {OperationName} completed successfully in {Duration}ms", 
                        correlationId, context.User, context.OperationName, duration.TotalMilliseconds);
                }
                else
                {
                    _logger.LogError("[OPERATION_END] [CID:{CorrelationId}] [User:{User}] {OperationName} failed in {Duration}ms: {Error}", 
                        correlationId, context.User, context.OperationName, duration.TotalMilliseconds, errorMessage ?? "Unknown error");
                }

                // Update performance metrics
                UpdatePerformanceMetrics(context.OperationType, duration, success);
            }
            else
            {
                _logger.LogWarning("[OPERATION_END] [CID:{CorrelationId}] Operation context not found", correlationId);
            }
        }

        public IDisposable MeasureOperation(string correlationId, string operationName)
        {
            return new OperationMeasurer(this, correlationId, operationName);
        }

        public void LogPerformanceWarning(string operationName, TimeSpan duration, TimeSpan threshold)
        {
            var correlationId = GenerateCorrelationId("PERF");
            _logger.LogWarning("[PERFORMANCE_WARNING] [CID:{CorrelationId}] {OperationName} took {Duration}ms (threshold: {Threshold}ms)", 
                correlationId, operationName, duration.TotalMilliseconds, threshold.TotalMilliseconds);
        }

        public void LogStartupSummary(TimeSpan startupDuration, Dictionary<string, TimeSpan> componentDurations)
        {
            var correlationId = GenerateCorrelationId("STARTUP");
            
            _logger.LogInformation("[STARTUP_SUMMARY] [CID:{CorrelationId}] Application started in {TotalDuration}ms", 
                correlationId, startupDuration.TotalMilliseconds);

            foreach (var component in componentDurations)
            {
                _logger.LogInformation("[STARTUP_COMPONENT] [CID:{CorrelationId}] {Component}: {Duration}ms", 
                    correlationId, component.Key, component.Value.TotalMilliseconds);
            }

            // Log performance warnings for slow components
            foreach (var component in componentDurations.Where(c => c.Value.TotalMilliseconds > 1000))
            {
                _logger.LogWarning("[STARTUP_WARNING] [CID:{CorrelationId}] Slow component {Component}: {Duration}ms", 
                    correlationId, component.Key, component.Value.TotalMilliseconds);
            }
        }

        public PerformanceMetrics GetPerformanceMetrics(string operationType)
        {
            return _performanceMetrics.GetOrAdd(operationType, new PerformanceMetrics
            {
                OperationType = operationType
            });
        }

        public Dictionary<string, PerformanceMetrics> GetAllPerformanceMetrics()
        {
            return _performanceMetrics.ToDictionary(kvp => kvp.Key, kvp => kvp.Value);
        }

        public OperationContext[] GetActiveOperations()
        {
            return _activeOperations.Values.ToArray();
        }

        public void LogUserActivity(string activity, string? details = null)
        {
            var correlationId = GenerateCorrelationId("USER");
            var user = _currentUserService.Username ?? "<unknown>";
            
            if (!string.IsNullOrEmpty(details))
            {
                _logger.LogInformation("[USER_ACTIVITY] [CID:{CorrelationId}] [User:{User}] {Activity}: {Details}", 
                    correlationId, user, activity, details);
            }
            else
            {
                _logger.LogInformation("[USER_ACTIVITY] [CID:{CorrelationId}] [User:{User}] {Activity}", 
                    correlationId, user, activity);
            }
        }

        private void UpdatePerformanceMetrics(string operationType, TimeSpan duration, bool success)
        {
            var metrics = _performanceMetrics.GetOrAdd(operationType, new PerformanceMetrics
            {
                OperationType = operationType
            });

            metrics.TotalExecutions++;
            metrics.TotalDuration += duration;
            
            if (success)
            {
                metrics.SuccessfulExecutions++;
            }
            else
            {
                metrics.FailedExecutions++;
            }

            if (duration > metrics.MaxDuration)
            {
                metrics.MaxDuration = duration;
            }

            if (duration < metrics.MinDuration || metrics.MinDuration == TimeSpan.Zero)
            {
                metrics.MinDuration = duration;
            }

            metrics.AverageDuration = TimeSpan.FromTicks(metrics.TotalDuration.Ticks / metrics.TotalExecutions);
            metrics.SuccessRate = metrics.TotalExecutions > 0 ? (double)metrics.SuccessfulExecutions / metrics.TotalExecutions : 0;
            metrics.LastUpdated = DateTime.UtcNow;
        }
    }

    public interface IOperationalVisibilityService
    {
        string GenerateCorrelationId(string operationType = "OP");
        void LogOperationStart(string correlationId, string operationName, Dictionary<string, object>? parameters = null);
        void LogOperationEnd(string correlationId, bool success, string? errorMessage = null);
        IDisposable MeasureOperation(string correlationId, string operationName);
        void LogPerformanceWarning(string operationName, TimeSpan duration, TimeSpan threshold);
        void LogStartupSummary(TimeSpan startupDuration, Dictionary<string, TimeSpan> componentDurations);
        PerformanceMetrics GetPerformanceMetrics(string operationType);
        Dictionary<string, PerformanceMetrics> GetAllPerformanceMetrics();
        OperationContext[] GetActiveOperations();
        void LogUserActivity(string activity, string? details = null);
    }

    public class OperationContext
    {
        public string CorrelationId { get; set; } = string.Empty;
        public string OperationType { get; set; } = string.Empty;
        public string OperationName { get; set; } = string.Empty;
        public DateTime StartTime { get; set; }
        public string User { get; set; } = string.Empty;
        public Dictionary<string, object> Parameters { get; set; } = new();
    }

    public class PerformanceMetrics
    {
        public string OperationType { get; set; } = string.Empty;
        public int TotalExecutions { get; set; }
        public int SuccessfulExecutions { get; set; }
        public int FailedExecutions { get; set; }
        public TimeSpan TotalDuration { get; set; }
        public TimeSpan AverageDuration { get; set; }
        public TimeSpan MinDuration { get; set; }
        public TimeSpan MaxDuration { get; set; }
        public double SuccessRate { get; set; }
        public DateTime LastUpdated { get; set; }
    }

    public class OperationMeasurer : IDisposable
    {
        private readonly OperationalVisibilityService _service;
        private readonly string _correlationId;
        private readonly string _operationName;
        private readonly Stopwatch _stopwatch;

        public OperationMeasurer(OperationalVisibilityService service, string correlationId, string operationName)
        {
            _service = service;
            _correlationId = correlationId;
            _operationName = operationName;
            _stopwatch = Stopwatch.StartNew();
            
            _service.LogOperationStart(_correlationId, _operationName);
        }

        public void Dispose()
        {
            _stopwatch.Stop();
            _service.LogOperationEnd(_correlationId, true);
            
            // Check for performance warnings
            if (_stopwatch.ElapsedMilliseconds > 5000) // 5 second threshold
            {
                _service.LogPerformanceWarning(_operationName, _stopwatch.Elapsed, TimeSpan.FromMilliseconds(5000));
            }
        }
    }
}
