using System;
using System.Reflection;

namespace BestFlex.Application.Abstractions
{
    /// <summary>
    /// Provides canonical unwrapping of reflection exceptions to reveal root causes.
    /// TargetInvocationException is NEVER acceptable in user-facing error messages.
    /// </summary>
    public static class ReflectionExceptionUnwrapper
    {
        /// <summary>
        /// Unwraps TargetInvocationException recursively to return the real root exception.
        /// Preserves stack trace and original exception type when possible.
        /// </summary>
        /// <param name="exception">The exception to unwrap</param>
        /// <returns>The real root exception with preserved context</returns>
        public static Exception Unwrap(Exception exception)
        {
            if (exception == null)
                throw new ArgumentNullException(nameof(exception));

            var current = exception;
            
            // Unwrap TargetInvocationException recursively
            while (current is TargetInvocationException tie && tie.InnerException != null)
            {
                current = tie.InnerException;
            }

            // If we unwrapped to a different exception, preserve context
            if (current != exception)
            {
                // Create a new exception that preserves the original message but uses the unwrapped type
                // This ensures we don't lose the real error type while maintaining context
                if (current.GetType() != exception.GetType())
                {
                    // For UserFriendlyException, preserve it exactly
                    if (current is UserFriendlyException)
                        return current;
                    
                    // For other exceptions, wrap to preserve context but reveal real type
                    return new ReflectionUnwrappedException(
                        $"Reflection error: {current.Message}",
                        current);
                }
            }

            return current;
        }

        /// <summary>
        /// Gets the user-friendly message from an exception, unwrapping reflection exceptions first.
        /// </summary>
        /// <param name="exception">The exception to process</param>
        /// <returns>User-friendly error message</returns>
        public static string GetUserFriendlyMessage(Exception exception)
        {
            var unwrapped = Unwrap(exception);
            
            // Check for UserFriendlyException first
            if (unwrapped is UserFriendlyException ufe)
                return ufe.Message;
            
            // Check for common permission errors
            if (unwrapped.Message.Contains("permission", StringComparison.OrdinalIgnoreCase) ||
                unwrapped.Message.Contains("unauthorized", StringComparison.OrdinalIgnoreCase) ||
                unwrapped.Message.Contains("access denied", StringComparison.OrdinalIgnoreCase))
                return "You do not have permission to perform this action. Please contact your administrator.";
            
            // Check for feature availability errors
            if (unwrapped.Message.Contains("feature", StringComparison.OrdinalIgnoreCase) ||
                unwrapped.Message.Contains("unavailable", StringComparison.OrdinalIgnoreCase))
                return "This feature is currently unavailable. Please try again later or contact support.";
            
            // Check for service dependency errors
            if (unwrapped.Message.Contains("service", StringComparison.OrdinalIgnoreCase) ||
                unwrapped.Message.Contains("dependency", StringComparison.OrdinalIgnoreCase))
                return "A required service is not available. Please contact your administrator.";
            
            // Check for missing type/assembly errors
            if (unwrapped is TypeLoadException || unwrapped is FileNotFoundException)
                return $"Required component is missing: {unwrapped.Message}";
            
            // Default specific message
            return $"Operation failed: {unwrapped.Message}";
        }
    }

    /// <summary>
    /// Exception wrapper that indicates a reflection exception was unwrapped.
    /// This preserves the original exception context while revealing the real error.
    /// </summary>
    public class ReflectionUnwrappedException : Exception
    {
        public ReflectionUnwrappedException(string message, Exception innerException) 
            : base(message, innerException)
        {
        }
    }
}
