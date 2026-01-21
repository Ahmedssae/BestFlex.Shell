using System;
using System.Threading.Tasks;
using BestFlex.Application.Abstractions;
using Microsoft.Extensions.Logging;

namespace BestFlex.Infrastructure.Services
{
    public sealed class ErrorService : IErrorService
    {
        private readonly IAuditService _audit;
        private readonly ILogger<ErrorService> _logger;

        public ErrorService(IAuditService audit, ILogger<ErrorService> logger)
        {
            _audit = audit ?? throw new ArgumentNullException(nameof(audit));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public void Handle(Exception ex, string context)
        {
            try
            {
                // Categorize error type
                var errorType = CategorizeError(ex);
                
                // Log to system logger
                _logger.LogError(ex, "Error in {Context}: {Message}", context, ex.Message);
                
                // Log to audit service for security events
                if (errorType == ErrorType.Security || errorType == ErrorType.Infrastructure)
                {
                    _ = Task.Run(async () => await _audit.LogSecurityAsync(
                        $"ERROR_{errorType.ToString().ToUpper()}",
                        $"Context: {context}, Error: {ex.Message}"
                    ));
                }
                
                // Never throw after handling
            }
            catch (Exception handlingEx)
            {
                // Last resort - don't let error handling crash the app
                try
                {
                    _logger.LogCritical(handlingEx, "Error handling failed for context: {Context}", context);
                }
                catch
                {
                    // Absolute last resort - swallow to prevent crash
                }
            }
        }

        public void HandleUserError(string message, string context)
        {
            try
            {
                _logger.LogWarning("User error in {Context}: {Message}", context, message);
                
                // Log user errors to audit for tracking
                _ = Task.Run(async () => await _audit.LogSecurityAsync(
                    "USER_ERROR",
                    $"Context: {context}, Message: {message}"
                ));
            }
            catch (Exception ex)
            {
                // Swallow to prevent error handling from causing issues
                try
                {
                    _logger.LogCritical(ex, "Failed to handle user error in context: {Context}", context);
                }
                catch
                {
                    // Absolute last resort
                }
            }
        }

        private ErrorType CategorizeError(Exception ex)
        {
            if (ex is UnauthorizedAccessException || ex is System.Security.SecurityException)
                return ErrorType.Security;
            
            if (ex is ArgumentException || ex is InvalidOperationException || ex is System.ComponentModel.DataAnnotations.ValidationException)
                return ErrorType.Validation;
            
            if (ex is System.Data.DataException || ex is Microsoft.EntityFrameworkCore.DbUpdateException)
                return ErrorType.Infrastructure;
            
            if (ex is System.IO.IOException || ex is System.Net.WebException)
                return ErrorType.Infrastructure;
            
            return ErrorType.Business;
        }

        private enum ErrorType
        {
            Validation,
            Business,
            Infrastructure,
            Security
        }
    }
}
