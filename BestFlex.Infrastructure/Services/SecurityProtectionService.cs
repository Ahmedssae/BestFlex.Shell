using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Logging;
using BestFlex.Application.Abstractions;

namespace BestFlex.Infrastructure.Services
{
    /// <summary>
    /// Provides security protection for data logging and permission enforcement
    /// </summary>
    public class SecurityProtectionService : ISecurityProtectionService
    {
        private readonly ILogger<SecurityProtectionService> _logger;
        private readonly ICurrentUserService _currentUserService;
        
        // Patterns to detect sensitive information
        private static readonly Regex[] SensitivePatterns = new[]
        {
            new Regex(@"(?i)(password|pwd|pass)\s*[:=]\s*[^\s]+", RegexOptions.Compiled),
            new Regex(@"(?i)(token|key|secret)\s*[:=]\s*[^\s]+", RegexOptions.Compiled),
            new Regex(@"(?i)(credit\s*card|cc)\s*[:=]\s*\d{4,}", RegexOptions.Compiled),
            new Regex(@"(?i)(ssn|social\s*security)\s*[:=]\s*\d{3}-\d{2}-\d{4}", RegexOptions.Compiled),
            new Regex(@"(?i)(account\s*number|account\s*#)\s*[:=]\s*\d{8,}", RegexOptions.Compiled),
            new Regex(@"(?i)(email)\s*[:=]\s*[^\s@]+@[^\s@]+\.[^\s@]+", RegexOptions.Compiled),
            new Regex(@"(?i)(phone|tel)\s*[:=]\s*\d{3}[-.\s]\d{3}[-.\s]\d{4}", RegexOptions.Compiled)
        };

        public SecurityProtectionService(
            ILogger<SecurityProtectionService> logger,
            ICurrentUserService currentUserService)
        {
            _logger = logger;
            _currentUserService = currentUserService;
        }

        public string SanitizeLogMessage(string message)
        {
            if (string.IsNullOrEmpty(message))
                return message;

            var sanitized = message;
            
            // Remove sensitive information patterns
            foreach (var pattern in SensitivePatterns)
            {
                sanitized = pattern.Replace(sanitized, m => $"{m.Groups[1].Value}: [REDACTED]");
            }

            // Additional sanitization for common sensitive data formats
            sanitized = Regex.Replace(sanitized, @"\b\d{4}[-\s]?\d{4}[-\s]?\d{4}[-\s]?\d{4}\b", "[CARD-REDACTED]", RegexOptions.Compiled);
            sanitized = Regex.Replace(sanitized, @"\b\d{3}-\d{2}-\d{4}\b", "[SSN-REDACTED]", RegexOptions.Compiled);
            sanitized = Regex.Replace(sanitized, @"\b[A-Za-z0-9._%+-]+@[A-Za-z0-9.-]+\.[A-Z|a-z]{2,}\b", "[EMAIL-REDACTED]", RegexOptions.Compiled);

            return sanitized;
        }

        public void LogSecureInformation(LogLevel level, string message, Exception? exception = null, params object[] args)
        {
            var currentUser = _currentUserService.Username ?? "<unknown>";
            var sanitizedMessage = SanitizeLogMessage(message);
            var sanitizedArgs = args.Select(arg => arg is string str ? SanitizeLogMessage(str) : arg).ToArray();

            // Add user context to log message
            var contextualMessage = $"[User:{currentUser}] {sanitizedMessage}";

            switch (level)
            {
                case LogLevel.Debug:
                    _logger.LogDebug(exception, contextualMessage, sanitizedArgs);
                    break;
                case LogLevel.Information:
                    _logger.LogInformation(exception, contextualMessage, sanitizedArgs);
                    break;
                case LogLevel.Warning:
                    _logger.LogWarning(exception, contextualMessage, sanitizedArgs);
                    break;
                case LogLevel.Error:
                    _logger.LogError(exception, contextualMessage, sanitizedArgs);
                    break;
                case LogLevel.Critical:
                    _logger.LogCritical(exception, contextualMessage, sanitizedArgs);
                    break;
            }
        }

        public SecurityCheckResult CheckPermission(string permission, string? resourceName = null)
        {
            try
            {
                var currentUser = _currentUserService.Username ?? "<anonymous>";
                
                // Check if user is authenticated
                if (string.IsNullOrEmpty(currentUser) || currentUser == "<anonymous>")
                {
                    LogSecurityEvent("PERMISSION_DENIED", $"User not authenticated for permission: {permission}", resourceName);
                    return new SecurityCheckResult
                    {
                        IsAllowed = false,
                        Reason = "User not authenticated",
                        Permission = permission,
                        ResourceName = resourceName
                    };
                }

                // Check for admin override (admin users have all permissions)
                if (IsAdminUser(currentUser))
                {
                    LogSecurityEvent("PERMISSION_GRANTED", $"Admin user granted permission: {permission}", resourceName);
                    return new SecurityCheckResult
                    {
                        IsAllowed = true,
                        Reason = "Admin user",
                        Permission = permission,
                        ResourceName = resourceName
                    };
                }

                // Check specific permission (simplified for demo - in real implementation, check against user roles/permissions)
                var isAllowed = CheckUserPermission(currentUser, permission);

                if (isAllowed)
                {
                    LogSecurityEvent("PERMISSION_GRANTED", $"User granted permission: {permission}", resourceName);
                    return new SecurityCheckResult
                    {
                        IsAllowed = true,
                        Reason = "Permission granted",
                        Permission = permission,
                        ResourceName = resourceName
                    };
                }
                else
                {
                    LogSecurityEvent("PERMISSION_DENIED", $"User denied permission: {permission}", resourceName);
                    return new SecurityCheckResult
                    {
                        IsAllowed = false,
                        Reason = "Insufficient permissions",
                        Permission = permission,
                        ResourceName = resourceName
                    };
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error checking permission {Permission} for user {User}", permission, _currentUserService.Username);
                return new SecurityCheckResult
                {
                    IsAllowed = false,
                    Reason = "Error checking permission",
                    Permission = permission,
                    ResourceName = resourceName
                };
            }
        }

        public void LogSecurityEvent(string eventType, string description, string? resourceName = null)
        {
            var currentUser = _currentUserService.Username ?? "<unknown>";
            var timestamp = DateTime.UtcNow;
            var correlationId = GenerateSecurityCorrelationId();

            // Log security event without sensitive data
            var sanitizedDescription = SanitizeLogMessage(description);
            var logMessage = $"[SECURITY] [CID:{correlationId}] [{eventType}] [User:{currentUser}] {sanitizedDescription}";
            
            if (!string.IsNullOrEmpty(resourceName))
            {
                logMessage += $" [Resource:{resourceName}]";
            }

            _logger.LogInformation(logMessage);

            // In a real implementation, this would also write to a security audit log
            // For now, we just log to the standard logger
        }

        public void LogDataAccess(string operation, string tableName, int recordCount, bool success)
        {
            var currentUser = _currentUserService.Username ?? "<unknown>";
            var correlationId = GenerateSecurityCorrelationId();
            
            var logMessage = $"[DATA_ACCESS] [CID:{correlationId}] [User:{currentUser}] {operation} {tableName} ({recordCount} records) - {(success ? "SUCCESS" : "FAILED")}";
            
            _logger.LogInformation(logMessage);
        }

        public void EnforcePermission(string permission, string? resourceName = null)
        {
            var result = CheckPermission(permission, resourceName);
            
            if (!result.IsAllowed)
            {
                var currentUser = _currentUserService.Username ?? "<unknown>";
                var correlationId = GenerateSecurityCorrelationId();
                
                _logger.LogWarning("[SECURITY_VIOLATION] [CID:{CorrelationId}] [User:{User}] Attempted unauthorized access: {Permission} on {Resource}", 
                    correlationId, currentUser, permission, resourceName ?? "<unknown>");
                
                throw new UnauthorizedAccessException($"Access denied. User '{currentUser}' does not have permission '{permission}'{(resourceName != null ? $" for resource '{resourceName}'" : "")}. [CID:{correlationId}]");
            }
        }

        private bool IsAdminUser(string username)
        {
            // Simple admin check - in real implementation, check against user roles
            return username.Equals("admin", StringComparison.OrdinalIgnoreCase) || 
                   username.Equals("administrator", StringComparison.OrdinalIgnoreCase);
        }

        private bool CheckUserPermission(string username, string permission)
        {
            // Simplified permission check - in real implementation, check against user roles/permissions database
            // For demo purposes, allow basic permissions for non-admin users
            var basicPermissions = new[] { "read", "view", "list", "search" };
            return basicPermissions.Contains(permission.ToLowerInvariant());
        }

        private string GenerateSecurityCorrelationId()
        {
            var guidStr = Guid.NewGuid().ToString("N");
            return $"SEC-{DateTime.UtcNow:yyyyMMdd-HHmmss}-{guidStr.Substring(0, 8).ToUpper()}";
        }
    }

    public interface ISecurityProtectionService
    {
        string SanitizeLogMessage(string message);
        void LogSecureInformation(LogLevel level, string message, Exception? exception = null, params object[] args);
        SecurityCheckResult CheckPermission(string permission, string? resourceName = null);
        void LogSecurityEvent(string eventType, string description, string? resourceName = null);
        void LogDataAccess(string operation, string tableName, int recordCount, bool success);
        void EnforcePermission(string permission, string? resourceName = null);
    }

    public class SecurityCheckResult
    {
        public bool IsAllowed { get; set; }
        public string Reason { get; set; } = string.Empty;
        public string Permission { get; set; } = string.Empty;
        public string? ResourceName { get; set; }
        public DateTime CheckedAt { get; set; } = DateTime.UtcNow;
    }
}
