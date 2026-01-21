using System;

namespace BestFlex.Application
{
    /// <summary>
    /// Exception type intended to carry user-friendly messages that may be shown directly to users.
    /// Thrown by business/gate checks to signal recoverable user-facing conditions.
    /// </summary>
    public sealed class UserFriendlyException : Exception
    {
        public UserFriendlyException() { }
        public UserFriendlyException(string message) : base(message) { }
        public UserFriendlyException(string message, Exception inner) : base(message, inner) { }
    }
}
