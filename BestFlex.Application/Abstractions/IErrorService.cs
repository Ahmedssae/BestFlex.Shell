using System;

namespace BestFlex.Application.Abstractions
{
    /// <summary>
    /// Centralized error handling surface used by ViewModels.
    /// Implementations should log exceptions, categorize errors, and never rethrow.
    /// This interface is stable and should not be modified without architectural review.
    /// </summary>
    public interface IErrorService
    {
        /// <summary>Handle an exception with context information.</summary>
        void Handle(Exception ex, string context);
        
        /// <summary>Handle a user-facing error message.</summary>
        void HandleUserError(string message, string context);
    }

    /// <summary>
    /// User notification service for UI-specific operations.
    /// Implementations should show MessageBox dialogs and never throw.
    /// This interface is stable and should not be modified without architectural review.
    /// </summary>
    public interface IUserNotificationService
    {
        /// <summary>Show an error message to the user.</summary>
        void ShowError(string message);
        
        /// <summary>Show a warning message to the user.</summary>
        void ShowWarning(string message);
        
        /// <summary>Show an informational message to the user.</summary>
        void ShowInfo(string message);
    }
}
