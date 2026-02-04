using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using Microsoft.Extensions.Logging;
using BestFlex.Application.Abstractions;

namespace BestFlex.Shell.Services
{
    /// <summary>
    /// Provides operation correlation IDs and traceability across all layers
    /// </summary>
    public class CorrelationService : ICorrelationService
    {
        private readonly ILogger<CorrelationService> _logger;
        private readonly ICurrentUserService _currentUserService;
        private readonly AsyncLocal<CorrelationContext?> _currentContext = new();

        public CorrelationService(
            ILogger<CorrelationService> logger,
            ICurrentUserService currentUserService)
        {
            _logger = logger;
            _currentUserService = currentUserService;
        }

        public CorrelationContext CurrentContext => _currentContext.Value ?? new CorrelationContext();

        public CorrelationContext StartOperation(string operationType, string operationName, Dictionary<string, object>? parameters = null)
        {
            var context = new CorrelationContext
            {
                OperationId = GenerateOperationId(),
                ParentOperationId = CurrentContext.OperationId,
                OperationType = operationType,
                OperationName = operationName,
                UserId = _currentUserService.UserId,
                Username = _currentUserService.Username,
                StartTime = DateTime.UtcNow,
                Parameters = parameters ?? new Dictionary<string, object>(),
                StackTrace = new StackTrace().ToString()
            };

            _currentContext.Value = context;

            _logger.LogInformation("[OPERATION_START] [OID:{OperationId}] [PID:{ParentOperationId}] [User:{Username}] [Type:{OperationType}] {OperationName} started", 
                context.OperationId, context.ParentOperationId, context.Username, context.OperationType, context.OperationName);

            if (context.Parameters.Count > 0)
            {
                _logger.LogDebug("[OPERATION_PARAMS] [OID:{OperationId}] Parameters: {Parameters}", 
                    context.OperationId, string.Join(", ", context.Parameters.Select(kvp => $"{kvp.Key}={kvp.Value}")));
            }

            return context;
        }

        public void EndOperation(bool success, string? result = null, Dictionary<string, object>? resultContext = null)
        {
            var context = CurrentContext;
            if (string.IsNullOrEmpty(context.OperationId))
            {
                _logger.LogWarning("[OPERATION_END_NO_CONTEXT] Attempted to end operation without context");
                return;
            }

            context.EndTime = DateTime.UtcNow;
            context.Duration = context.EndTime - context.StartTime;
            context.Success = success;
            context.Result = result;
            context.ResultContext = resultContext ?? new Dictionary<string, object>();

            var logLevel = success ? LogLevel.Information : LogLevel.Warning;
            _logger.Log(logLevel, "[OPERATION_END] [OID:{OperationId}] [User:{Username}] [Type:{OperationType}] {OperationName} {Status} in {Duration}ms {Result}", 
                context.OperationId, context.Username, context.OperationType, context.OperationName, 
                success ? "completed" : "failed", context.Duration?.TotalMilliseconds ?? 0, result ?? "");

            if (context.ResultContext.Count > 0)
            {
                _logger.LogDebug("[OPERATION_RESULT] [OID:{OperationId}] Result context: {ResultContext}", 
                    context.OperationId, string.Join(", ", context.ResultContext.Select(kvp => $"{kvp.Key}={kvp.Value}")));
            }

            // Clear context if this is not a nested operation
            if (context.ParentOperationId == null)
            {
                _currentContext.Value = null;
            }
        }

        public void AddOperationParameter(string key, object value)
        {
            var context = CurrentContext;
            if (string.IsNullOrEmpty(context.OperationId))
            {
                _logger.LogWarning("[OPERATION_PARAM_NO_CONTEXT] Attempted to add parameter without context: {Key}={Value}", key, value);
                return;
            }

            context.Parameters[key] = value;
            _logger.LogDebug("[OPERATION_PARAM_ADDED] [OID:{OperationId}] {Key}={Value}", context.OperationId, key, value);
        }

        public void AddOperationResult(string key, object value)
        {
            var context = CurrentContext;
            if (string.IsNullOrEmpty(context.OperationId))
            {
                _logger.LogWarning("[OPERATION_RESULT_NO_CONTEXT] Attempted to add result without context: {Key}={Value}", key, value);
                return;
            }

            context.ResultContext[key] = value;
            _logger.LogDebug("[OPERATION_RESULT_ADDED] [OID:{OperationId}] {Key}={Value}", context.OperationId, key, value);
        }

        public CorrelationContext CreateChildOperation(string childOperationType, string childOperationName, Dictionary<string, object>? parameters = null)
        {
            var parentContext = CurrentContext;
            var childContext = new CorrelationContext
            {
                OperationId = GenerateOperationId(),
                ParentOperationId = parentContext.OperationId,
                OperationType = childOperationType,
                OperationName = childOperationName,
                UserId = _currentUserService.UserId,
                Username = _currentUserService.Username,
                StartTime = DateTime.UtcNow,
                Parameters = parameters ?? new Dictionary<string, object>(),
                StackTrace = new StackTrace().ToString()
            };

            _currentContext.Value = childContext;

            _logger.LogInformation("[CHILD_OPERATION_START] [OID:{OperationId}] [PID:{ParentOperationId}] [User:{Username}] [Type:{OperationType}] {OperationName} started", 
                childContext.OperationId, childContext.ParentOperationId, childContext.Username, childContext.OperationType, childContext.OperationName);

            return childContext;
        }

        public void LogOperationEvent(string eventType, string message, Dictionary<string, object>? context = null)
        {
            var operationContext = CurrentContext;
            var logContext = new Dictionary<string, object>
            {
                ["EventType"] = eventType,
                ["Message"] = message
            };

            if (!string.IsNullOrEmpty(operationContext.OperationId))
            {
                logContext["OperationId"] = operationContext.OperationId;
                logContext["OperationType"] = operationContext.OperationType;
                logContext["OperationName"] = operationContext.OperationName;
            }

            if (context != null)
            {
                foreach (var kvp in context)
                {
                    logContext[$"Event_{kvp.Key}"] = kvp.Value;
                }
            }

            _logger.LogInformation("[OPERATION_EVENT] [OID:{OperationId}] [Type:{EventType}] {Message}", 
                operationContext.OperationId ?? "<none>", eventType, message);

            if (context?.Count > 0)
            {
                _logger.LogDebug("[OPERATION_EVENT_CONTEXT] [OID:{OperationId}] Event context: {Context}", 
                    operationContext.OperationId ?? "<none>", string.Join(", ", context.Select(kvp => $"{kvp.Key}={kvp.Value}")));
            }
        }

        public string GenerateOperationId()
        {
            return $"OP_{DateTime.UtcNow:yyyyMMddHHmmss}_{Guid.NewGuid().ToString("N")[..8]}";
        }

        public CorrelationContext? GetOperationContext(string operationId)
        {
            // In a real implementation, this would query a persistent store
            // For now, return current context if it matches
            var current = CurrentContext;
            return current.OperationId == operationId ? current : null;
        }

        public IEnumerable<CorrelationContext> GetActiveOperations()
        {
            // In a real implementation, this would query a persistent store
            // For now, return current context if active
            var current = CurrentContext;
            if (!string.IsNullOrEmpty(current.OperationId) && current.EndTime == null)
            {
                yield return current;
            }
        }

        public void SetOperationContext(CorrelationContext context)
        {
            _currentContext.Value = context;
            _logger.LogDebug("[OPERATION_CONTEXT_SET] [OID:{OperationId}] Context set for operation", context.OperationId);
        }
    }

    public interface ICorrelationService
    {
        CorrelationContext CurrentContext { get; }
        CorrelationContext StartOperation(string operationType, string operationName, Dictionary<string, object>? parameters = null);
        void EndOperation(bool success, string? result = null, Dictionary<string, object>? resultContext = null);
        void AddOperationParameter(string key, object value);
        void AddOperationResult(string key, object value);
        CorrelationContext CreateChildOperation(string childOperationType, string childOperationName, Dictionary<string, object>? parameters = null);
        void LogOperationEvent(string eventType, string message, Dictionary<string, object>? context = null);
        string GenerateOperationId();
        CorrelationContext? GetOperationContext(string operationId);
        IEnumerable<CorrelationContext> GetActiveOperations();
        void SetOperationContext(CorrelationContext context);
    }

    public class CorrelationContext
    {
        public string OperationId { get; set; } = string.Empty;
        public string? ParentOperationId { get; set; }
        public string OperationType { get; set; } = string.Empty;
        public string OperationName { get; set; } = string.Empty;
        public Guid? UserId { get; set; }
        public string? Username { get; set; }
        public DateTime StartTime { get; set; }
        public DateTime? EndTime { get; set; }
        public TimeSpan? Duration { get; set; }
        public bool Success { get; set; }
        public string? Result { get; set; }
        public Dictionary<string, object> Parameters { get; set; } = new();
        public Dictionary<string, object> ResultContext { get; set; } = new();
        public string? StackTrace { get; set; }
        public bool IsActive => EndTime == null;
    }

    /// <summary>
    /// Helper for creating correlated operations with automatic cleanup
    /// </summary>
    public class CorrelatedOperation : IDisposable
    {
        private readonly ICorrelationService _correlationService;
        private readonly CorrelationContext _context;
        private readonly bool _isChild;

        public CorrelatedOperation(ICorrelationService correlationService, string operationType, string operationName, Dictionary<string, object>? parameters = null, bool isChild = false)
        {
            _correlationService = correlationService;
            _isChild = isChild;

            _context = isChild 
                ? _correlationService.CreateChildOperation(operationType, operationName, parameters)
                : _correlationService.StartOperation(operationType, operationName, parameters);
        }

        public string OperationId => _context.OperationId;
        public CorrelationContext Context => _context;

        public void Complete(bool success = true, string? result = null, Dictionary<string, object>? resultContext = null)
        {
            _correlationService.EndOperation(success, result, resultContext);
        }

        public void AddParameter(string key, object value)
        {
            _correlationService.AddOperationParameter(key, value);
        }

        public void AddResult(string key, object value)
        {
            _correlationService.AddOperationResult(key, value);
        }

        public void LogEvent(string eventType, string message, Dictionary<string, object>? context = null)
        {
            _correlationService.LogOperationEvent(eventType, message, context);
        }

        public void Dispose()
        {
            if (_context.EndTime == null)
            {
                // Auto-complete with success if not explicitly completed
                Complete(true);
            }
        }
    }

    /// <summary>
    /// Extension methods for easy correlation usage
    /// </summary>
    public static class CorrelationExtensions
    {
        public static CorrelatedOperation StartCorrelatedOperation(this ICorrelationService correlationService, string operationType, string operationName, Dictionary<string, object>? parameters = null)
        {
            return new CorrelatedOperation(correlationService, operationType, operationName, parameters);
        }

        public static CorrelatedOperation StartChildOperation(this ICorrelationService correlationService, string operationType, string operationName, Dictionary<string, object>? parameters = null)
        {
            return new CorrelatedOperation(correlationService, operationType, operationName, parameters, isChild: true);
        }
    }
}
