using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Extensions.Logging;
using BestFlex.Application.Abstractions;
using BestFlex.Domain.Exceptions;

namespace BestFlex.Shell.Services
{
    /// <summary>
    /// Translates domain exceptions into user-friendly messages while logging full technical details
    /// </summary>
    public class DomainExceptionUserMessageMapper : IDomainExceptionUserMessageMapper
    {
        private readonly ILogger<DomainExceptionUserMessageMapper> _logger;
        private readonly ICurrentUserService _currentUserService;

        public DomainExceptionUserMessageMapper(
            ILogger<DomainExceptionUserMessageMapper> logger,
            ICurrentUserService currentUserService)
        {
            _logger = logger;
            _currentUserService = currentUserService;
        }

        public UserFriendlyError TranslateException(Exception exception)
        {
            var currentUser = _currentUserService.Username ?? "<unknown>";
            var correlationId = GenerateCorrelationId();

            // Log the full technical exception
            _logger.LogError(exception, "[DOMAIN_ERROR] [CID:{CorrelationId}] [User:{User}] Domain exception occurred: {ExceptionType} - {Message}", 
                correlationId, currentUser, exception.GetType().Name, exception.Message);

            // Translate to user-friendly message
            var userMessage = GetUserFriendlyMessage(exception);
            
            return new UserFriendlyError
            {
                UserMessage = userMessage,
                TechnicalMessage = exception.Message,
                CorrelationId = correlationId,
                ExceptionType = exception.GetType().Name,
                Timestamp = DateTime.UtcNow,
                User = currentUser
            };
        }

        private string GetUserFriendlyMessage(Exception exception)
        {
            return exception switch
            {
                // Stock/Inventory exceptions
                InsufficientStockException stockEx => ExtractStockMessage(stockEx.Message),
                
                // Customer/Credit exceptions
                CreditLimitExceededException creditEx => ExtractCreditLimitMessage(creditEx.Message),
                
                // Invoice exceptions
                InvoiceAlreadyPostedException invoiceEx => ExtractInvoicePostedMessage(invoiceEx.Message),
                
                // Period exceptions
                PeriodClosedException periodEx => ExtractPeriodClosedMessage(periodEx.Message),
                
                // Journal exceptions
                UnbalancedJournalEntryException journalEx => ExtractJournalMessage(journalEx.Message),
                
                // General domain exceptions
                DomainException domainEx => ExtractDomainMessage(domainEx.Message),
                
                // Permission/Authorization exceptions
                UnauthorizedAccessException authEx => "You don't have permission to perform this action. Please contact your administrator.",
                
                // Default fallback - never expose technical details to users
                _ => "An unexpected error occurred. Please try again. If the problem persists, contact support."
            };
        }

        private string ExtractStockMessage(string technicalMessage)
        {
            // Extract product name and quantities from technical message
            if (technicalMessage.Contains("Insufficient stock"))
            {
                return "There is insufficient stock available for this product. Please check inventory levels or reduce the quantity.";
            }
            return "Stock quantity is insufficient for this operation.";
        }

        private string ExtractCreditLimitMessage(string technicalMessage)
        {
            if (technicalMessage.Contains("credit limit"))
            {
                return "Customer has exceeded their credit limit. Please check customer credit status or reduce the invoice amount.";
            }
            return "Customer credit limit has been exceeded.";
        }

        private string ExtractInvoicePostedMessage(string technicalMessage)
        {
            if (technicalMessage.Contains("already posted"))
            {
                return "This invoice has already been posted and cannot be modified.";
            }
            return "Invoice cannot be modified in its current state.";
        }

        private string ExtractPeriodClosedMessage(string technicalMessage)
        {
            if (technicalMessage.Contains("period closed"))
            {
                return "This accounting period is closed. Please select an open period for this transaction.";
            }
            return "The selected period is closed for transactions.";
        }

        private string ExtractJournalMessage(string technicalMessage)
        {
            if (technicalMessage.Contains("unbalanced"))
            {
                return "Journal entry is not balanced. Please ensure debits equal credits.";
            }
            return "Journal entry validation failed.";
        }

        private string ExtractDomainMessage(string technicalMessage)
        {
            // Check for common patterns in domain messages
            var message = technicalMessage.ToLowerInvariant();
            
            if (message.Contains("not found"))
                return "The requested record was not found.";
            
            if (message.Contains("already exists"))
                return "A record with these details already exists.";
            
            if (message.Contains("invalid"))
                return "The provided data is invalid. Please check your input.";
            
            // Return a cleaned version of the domain message if it seems user-friendly
            if (technicalMessage.Length < 150 && !technicalMessage.Contains("Exception") && !technicalMessage.Contains("Error"))
            {
                return technicalMessage;
            }
            
            return "A business rule validation failed. Please check your data and try again.";
        }

        public void LogExceptionWithUserContext(Exception exception, string operation, Dictionary<string, object>? context = null)
        {
            var currentUser = _currentUserService.Username ?? "<unknown>";
            var correlationId = GenerateCorrelationId();

            _logger.LogError(exception, 
                "[DOMAIN_ERROR] [CID:{CorrelationId}] [User:{User}] [Operation:{Operation}] Domain exception occurred: {ExceptionType} - {Message} {Context}", 
                correlationId, currentUser, operation, exception.GetType().Name, exception.Message, 
                context != null ? $"Context: {string.Join(", ", context.Select(kvp => $"{kvp.Key}={kvp.Value}"))}" : "");
        }

        public bool IsUserFriendlyException(Exception exception)
        {
            return exception is DomainException ||
                   exception is BusinessRuleViolationException ||
                   (exception.Message.Length < 200 && !exception.Message.Contains("Exception") && !exception.Message.Contains("Error"));
        }

        private string GenerateCorrelationId()
        {
            var guidStr = Guid.NewGuid().ToString("N");
            return $"ERR-{DateTime.UtcNow:yyyyMMdd-HHmmss}-{guidStr.Substring(0, 8).ToUpper()}";
        }
    }

    public interface IDomainExceptionUserMessageMapper
    {
        UserFriendlyError TranslateException(Exception exception);
        void LogExceptionWithUserContext(Exception exception, string operation, Dictionary<string, object>? context = null);
        bool IsUserFriendlyException(Exception exception);
    }

    public class UserFriendlyError
    {
        public string UserMessage { get; set; } = string.Empty;
        public string TechnicalMessage { get; set; } = string.Empty;
        public string CorrelationId { get; set; } = string.Empty;
        public string ExceptionType { get; set; } = string.Empty;
        public DateTime Timestamp { get; set; }
        public string User { get; set; } = string.Empty;
        
        public string GetDisplayMessage()
        {
            return $"{UserMessage}\n\nError ID: {CorrelationId}";
        }
    }
}
