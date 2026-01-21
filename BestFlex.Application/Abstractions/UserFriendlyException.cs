using System;

namespace BestFlex.Application.Abstractions
{
    public sealed class UserFriendlyException : Exception
    {
        public UserFriendlyException(string message) : base(message) { }
        public UserFriendlyException(string message, Exception innerException) : base(message, innerException) { }
    }
}
