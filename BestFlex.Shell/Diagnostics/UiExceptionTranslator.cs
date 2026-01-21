using System;
using BestFlex.Application.Abstractions;

namespace BestFlex.Shell.Diagnostics
{
    /// <summary>
    /// Translates exceptions into user-safe messages for UI display.
    /// </summary>
    public sealed class UiExceptionTranslator
    {
        private const string GenericMessage = "An unexpected error occurred. Please contact support if the problem persists.";

        /// <summary>
        /// Translate an exception into a message safe to show to end users.
        /// </summary>
        /// <param name="ex">The exception to translate.</param>
        /// <returns>User-safe message.</returns>
        public string Translate(Exception ex)
        {
            if (ex == null) return GenericMessage;

            if (ex is UserFriendlyException uf)
            {
                return uf.Message ?? string.Empty;
            }

            return GenericMessage;
        }
    }
}
