using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using BestFlex.Application.Abstractions;

namespace BestFlex.Shell.Services
{
    /// <summary>
    /// Enforces structured logging with proper context and discipline
    /// </summary>
    public class StructuredLoggingService : IStructuredLoggingService
    {
        private readonly ILogger<StructuredLoggingService> _logger;
        private readonly ICorrelationService _correlationService;
        private readonly ICurrentUserService _currentUserService;

        public StructuredLoggingService(
            ILogger<StructuredLoggingService> logger,
            ICorrelationService correlationService,
            ICurrentUserService currentUserService)
        {
            _logger = logger;
            _correlationService = correlationService;
            _currentUserService = currentUserService;
        }

        public void LogBusinessOperation(string operation, string entity, string? entityId = null, Dictionary<string, object>? additionalContext = null)
        {
            var context = CreateLogContext(operation, entity, entityId, additionalContext);
            
            _logger.LogInformation("[BUSINESS_OPERATION] [OID:{OperationId}] [User:{Username}] [Operation:{Operation}] [Entity:{Entity}] [EntityId:{EntityId}] Business operation executed", 
                context.OperationId, context.Username, operation, entity, entityId ?? "<none>");

            if (additionalContext?.Count > 0)
            {
                _logger.LogDebug("[BUSINESS_OPERATION_CONTEXT] [OID:{OperationId}] Additional context: {Context}", 
                    context.OperationId, SerializeContext(additionalContext));
            }
        }

        public void LogDataAccess(string operation, string table, string? recordId = null, Dictionary<string, object>? parameters = null, long? executionTimeMs = null)
        {
            var context = CreateLogContext(operation, table, recordId, parameters);
            
            _logger.LogInformation("[DATA_ACCESS] [OID:{OperationId}] [User:{Username}] [Operation:{Operation}] [Table:{Table}] [RecordId:{RecordId}] [Duration:{Duration}ms] Data access operation", 
                context.OperationId, context.Username, operation, table, recordId ?? "<none>", executionTimeMs ?? 0);

            if (parameters?.Count > 0)
            {
                _logger.LogDebug("[DATA_ACCESS_PARAMS] [OID:{OperationId}] Parameters: {Parameters}", 
                    context.OperationId, SerializeContext(parameters));
            }
        }

        public void LogSecurityEvent(string eventType, string resource, string? resourceId = null, bool success = true, string? details = null)
        {
            var context = CreateLogContext(eventType, resource, resourceId);
            var level = success ? LogLevel.Information : LogLevel.Warning;
            
            _logger.Log(level, "[SECURITY_EVENT] [OID:{OperationId}] [User:{Username}] [EventType:{EventType}] [Resource:{Resource}] [ResourceId:{ResourceId}] [Success:{Success}] Security event: {Details}", 
                context.OperationId, context.Username, eventType, resource, resourceId ?? "<none>", success, details ?? "");
        }

        public void LogPerformanceMetric(string metric, double value, string? unit = null, Dictionary<string, object>? dimensions = null)
        {
            var context = CreateLogContext(metric, "Performance", null, dimensions);
            
            _logger.LogInformation("[PERFORMANCE_METRIC] [OID:{OperationId}] [User:{Username}] [Metric:{Metric}] [Value:{Value}] [Unit:{Unit}] Performance metric recorded", 
                context.OperationId, context.Username, metric, value, unit ?? "");

            if (dimensions?.Count > 0)
            {
                _logger.LogDebug("[PERFORMANCE_DIMENSIONS] [OID:{OperationId}] Dimensions: {Dimensions}", 
                    context.OperationId, SerializeContext(dimensions));
            }
        }

        public void LogError(Exception exception, string context, string operation, string? entity = null, Dictionary<string, object>? additionalContext = null)
        {
            var logContext = CreateLogContext("Error", operation, entity, additionalContext);
            
            _logger.LogError(exception, "[ERROR] [OID:{OperationId}] [User:{Username}] [Context:{Context}] [Operation:{Operation}] [Entity:{Entity}] Error occurred: {ErrorType} - {ErrorMessage}", 
                logContext.OperationId, logContext.Username, context, operation, entity ?? "<none>", exception.GetType().Name, exception.Message);

            if (additionalContext?.Count > 0)
            {
                _logger.LogDebug("[ERROR_CONTEXT] [OID:{OperationId}] Additional error context: {Context}", 
                    logContext.OperationId, SerializeContext(additionalContext));
            }
        }

        public void LogCriticalFailure(string failureType, string description, string? component = null, Dictionary<string, object>? failureContext = null)
        {
            var context = CreateLogContext(failureType, component ?? "System", null, failureContext);
            
            _logger.LogCritical("[CRITICAL_FAILURE] [OID:{OperationId}] [User:{Username}] [FailureType:{FailureType}] [Component:{Component}] Critical failure: {Description}", 
                context.OperationId, context.Username, failureType, component ?? "Unknown", description);

            if (failureContext?.Count > 0)
            {
                _logger.LogCritical("[CRITICAL_FAILURE_CONTEXT] [OID:{OperationId}] Failure context: {Context}", 
                    context.OperationId, SerializeContext(failureContext));
            }
        }

        public void LogSystemEvent(string eventType, string component, string? details = null, Dictionary<string, object>? systemContext = null)
        {
            var context = CreateLogContext(eventType, component, null, systemContext);
            
            _logger.LogInformation("[SYSTEM_EVENT] [OID:{OperationId}] [EventType:{EventType}] [Component:{Component}] System event: {Details}", 
                context.OperationId, eventType, component, details ?? "");

            if (systemContext?.Count > 0)
            {
                _logger.LogDebug("[SYSTEM_EVENT_CONTEXT] [OID:{OperationId}] System context: {Context}", 
                    context.OperationId, SerializeContext(systemContext));
            }
        }

        public void LogUserAction(string action, string? target = null, Dictionary<string, object>? actionContext = null)
        {
            var context = CreateLogContext(action, target ?? "UI", null, actionContext);
            
            _logger.LogInformation("[USER_ACTION] [OID:{OperationId}] [User:{Username}] [Action:{Action}] [Target:{Target}] User action performed", 
                context.OperationId, context.Username, action, target ?? "");

            if (actionContext?.Count > 0)
            {
                _logger.LogDebug("[USER_ACTION_CONTEXT] [OID:{OperationId}] Action context: {Context}", 
                    context.OperationId, SerializeContext(actionContext));
            }
        }

        public void LogValidationFailure(string validationType, string entity, string[] failures, Dictionary<string, object>? validationContext = null)
        {
            var context = CreateLogContext(validationType, entity, null, validationContext);
            
            _logger.LogWarning("[VALIDATION_FAILURE] [OID:{OperationId}] [User:{Username}] [ValidationType:{ValidationType}] [Entity:{Entity}] Validation failed: {Failures}", 
                context.OperationId, context.Username, validationType, entity, string.Join("; ", failures));

            if (validationContext?.Count > 0)
            {
                _logger.LogDebug("[VALIDATION_CONTEXT] [OID:{OperationId}] Validation context: {Context}", 
                    context.OperationId, SerializeContext(validationContext));
            }
        }

        public void LogIntegrationEvent(string system, string direction, string message, string? messageId = null, bool success = true, Dictionary<string, object>? integrationContext = null)
        {
            var context = CreateLogContext($"{direction}_{system}", "Integration", messageId, integrationContext);
            var level = success ? LogLevel.Information : LogLevel.Warning;
            
            _logger.Log(level, "[INTEGRATION_EVENT] [OID:{OperationId}] [User:{Username}] [System:{System}] [Direction:{Direction}] [MessageId:{MessageId}] [Success:{Success}] Integration: {Message}", 
                context.OperationId, context.Username, system, direction, messageId ?? "<none>", success, message);

            if (integrationContext?.Count > 0)
            {
                _logger.LogDebug("[INTEGRATION_CONTEXT] [OID:{OperationId}] Integration context: {Context}", 
                    context.OperationId, SerializeContext(integrationContext));
            }
        }

        public void LogAuditEvent(string auditType, string entity, string? entityId = null, Dictionary<string, object>? auditContext = null)
        {
            var context = CreateLogContext(auditType, entity, entityId, auditContext);
            
            _logger.LogInformation("[AUDIT_EVENT] [OID:{OperationId}] [User:{Username}] [AuditType:{AuditType}] [Entity:{Entity}] [EntityId:{EntityId}] Audit trail entry", 
                context.OperationId, context.Username, auditType, entity, entityId ?? "");

            if (auditContext?.Count > 0)
            {
                _logger.LogDebug("[AUDIT_CONTEXT] [OID:{OperationId}] Audit context: {Context}", 
                    context.OperationId, SerializeContext(auditContext));
            }
        }

        private LogContext CreateLogContext(string operation, string entity, string? entityId = null, Dictionary<string, object>? additionalContext = null)
        {
            return new LogContext
            {
                OperationId = _correlationService.CurrentContext.OperationId,
                Username = _currentUserService.Username,
                UserId = _currentUserService.UserId,
                Operation = operation,
                Entity = entity,
                EntityId = entityId,
                Timestamp = DateTime.UtcNow,
                AdditionalContext = additionalContext ?? new Dictionary<string, object>()
            };
        }

        private string SerializeContext(Dictionary<string, object> context)
        {
            try
            {
                return JsonSerializer.Serialize(context, new JsonSerializerOptions { WriteIndented = false });
            }
            catch
            {
                return string.Join(", ", context.Select(kvp => $"{kvp.Key}={kvp.Value}"));
            }
        }

        public void EnforceLoggingDiscipline()
        {
            // This method can be called during startup to ensure logging is properly configured
            _logger.LogInformation("[LOGGING_DISCIPLINE] [OID:{OperationId}] Structured logging service initialized with correlation support", 
                _correlationService.CurrentContext.OperationId);
        }
    }

    public interface IStructuredLoggingService
    {
        void LogBusinessOperation(string operation, string entity, string? entityId = null, Dictionary<string, object>? additionalContext = null);
        void LogDataAccess(string operation, string table, string? recordId = null, Dictionary<string, object>? parameters = null, long? executionTimeMs = null);
        void LogSecurityEvent(string eventType, string resource, string? resourceId = null, bool success = true, string? details = null);
        void LogPerformanceMetric(string metric, double value, string? unit = null, Dictionary<string, object>? dimensions = null);
        void LogError(Exception exception, string context, string operation, string? entity = null, Dictionary<string, object>? additionalContext = null);
        void LogCriticalFailure(string failureType, string description, string? component = null, Dictionary<string, object>? failureContext = null);
        void LogSystemEvent(string eventType, string component, string? details = null, Dictionary<string, object>? systemContext = null);
        void LogUserAction(string action, string? target = null, Dictionary<string, object>? actionContext = null);
        void LogValidationFailure(string validationType, string entity, string[] failures, Dictionary<string, object>? validationContext = null);
        void LogIntegrationEvent(string system, string direction, string message, string? messageId = null, bool success = true, Dictionary<string, object>? integrationContext = null);
        void LogAuditEvent(string auditType, string entity, string? entityId = null, Dictionary<string, object>? auditContext = null);
        void EnforceLoggingDiscipline();
    }

    internal class LogContext
    {
        public string OperationId { get; set; } = string.Empty;
        public string? Username { get; set; }
        public Guid? UserId { get; set; }
        public string Operation { get; set; } = string.Empty;
        public string Entity { get; set; } = string.Empty;
        public string? EntityId { get; set; }
        public DateTime Timestamp { get; set; }
        public Dictionary<string, object> AdditionalContext { get; set; } = new();
    }

    /// <summary>
    /// Attribute to enforce structured logging on methods
    /// </summary>
    public class StructuredLogOperationAttribute : Attribute
    {
        public string OperationType { get; set; } = "Business";
        public string Entity { get; set; } = "Unknown";
        public bool LogParameters { get; set; } = true;
        public bool LogResult { get; set; } = true;
    }

    /// <summary>
    /// Helper for structured logging with automatic correlation
    /// </summary>
    public static class StructuredLoggingExtensions
    {
        public static void LogWithCorrelation(this IStructuredLoggingService logger, string level, string message, Dictionary<string, object>? context = null)
        {
            var correlationContext = new Dictionary<string, object>
            {
                ["Message"] = message,
                ["Timestamp"] = DateTime.UtcNow
            };

            if (context != null)
            {
                foreach (var kvp in context)
                {
                    correlationContext[kvp.Key] = kvp.Value;
                }
            }

            // This would be implemented based on the specific logging level
            // For now, it's a placeholder for the pattern
        }
    }
}
