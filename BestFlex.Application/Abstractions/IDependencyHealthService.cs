using System;

namespace BestFlex.Application.Abstractions
{
    /// <summary>
    /// Validates critical dependency graph at startup.
    /// </summary>
    public interface IDependencyHealthService
    {
        /// <summary>
        /// Validate required services from the provided service provider.
        /// Throws <see cref="InvalidOperationException"/> describing the missing service.
        /// </summary>
        /// <param name="provider">Service provider</param>
        void Validate(IServiceProvider provider);
    }
}
