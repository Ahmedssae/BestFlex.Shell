using System;
using System.Linq;
using Microsoft.Extensions.DependencyInjection;
using BestFlex.Application.Abstractions;

namespace BestFlex.Infrastructure.Diagnostics
{
    /// <summary>
    /// Validates critical dependency graph at startup.
    /// Does not reference Shell assemblies; resolves types by name where necessary.
    /// </summary>
    public class DependencyHealthService : IDependencyHealthService
    {
        private static readonly string[] RequiredTypeNames = new[]
        {
            // Shell UI types - resolved by full name
            "BestFlex.Shell.ViewModels.LoginViewModel",
            "BestFlex.Shell.MainWindow",
            // Core services
            "BestFlex.Shell.Services.NavigationService", // shell-specific nav implementation
        };

        public void Validate(IServiceProvider provider)
        {
            if (provider == null) throw new ArgumentNullException(nameof(provider));
            // 1. Resolve application/service interfaces via DI directly
            TryResolve(provider, typeof(BestFlex.Application.Abstractions.ISalesService));
            TryResolve(provider, typeof(BestFlex.Application.Abstractions.IStockValidationService));
            TryResolve(provider, typeof(BestFlex.Application.Abstractions.IUnitOfWork));
            TryResolve(provider, typeof(BestFlex.Application.Abstractions.IAuthorizationService));
            TryResolve(provider, typeof(BestFlex.Application.Abstractions.INavigationService));

            // 2. Resolve by concrete types that live in other projects (Shell) using reflection to avoid a compile-time reference
            foreach (var fullName in RequiredTypeNames)
            {
                var t = FindType(fullName);
                if (t == null)
                {
                    if (fullName == "BestFlex.Shell.ViewModels.LoginViewModel" || fullName == "BestFlex.Shell.MainWindow")
                    {
                        ThrowMissing(fullName);
                    }
                    continue;
                }

                TryResolve(provider, t);
            }
        }

        private static void TryResolve(IServiceProvider provider, Type serviceType)
        {
            try
            {
                // Use non-generic GetRequiredService to ensure DI throws InvalidOperationException for missing registrations
                var method = typeof(ServiceProviderServiceExtensions).GetMethods()
                    .First(m => m.Name == "GetRequiredService" && m.IsGenericMethod && m.GetParameters().Length == 1);
                var generic = method.MakeGenericMethod(serviceType);
                try
                {
                    generic.Invoke(null, new object[] { provider });
                }
                catch (System.Reflection.TargetInvocationException tie) when (tie.InnerException is InvalidOperationException)
                {
                    // Rethrow with exact message
                    ThrowMissing(serviceType.FullName ?? serviceType.Name);
                }
            }
            catch (InvalidOperationException)
            {
                // Convert to required message
                ThrowMissing(serviceType.FullName ?? serviceType.Name);
            }
        }

        private static void ThrowMissing(string fullName)
        {
            var simple = fullName ?? string.Empty;
            var last = simple.LastIndexOf('.');
            if (last >= 0 && last < simple.Length - 1) simple = simple[(last + 1)..];
            var msg = "CRITICAL: Dependency injection validation failed." + Environment.NewLine +
                      "Missing or misconfigured service: " + simple;
            throw new InvalidOperationException(msg);
        }

        private static string? ExtractMissingTypeName(InvalidOperationException ex)
        {
            // Try to parse known message patterns from DI
            // Example: No service for type 'Namespace.Type' has been registered.
            var msg = ex.Message ?? string.Empty;
            var token = "No service for type '";
            var idx = msg.IndexOf(token, StringComparison.Ordinal);
            if (idx >= 0)
            {
                var start = idx + token.Length;
                var end = msg.IndexOf('\'', start);
                if (end > start)
                {
                    return msg[start..end];
                }
            }

            // Fallback: look for type names in inner exceptions
            if (ex.InnerException != null)
            {
                var inner = ex.InnerException.Message ?? string.Empty;
                foreach (var part in inner.Split(new[] { ' ', '\'', '"', '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries))
                {
                    if (part.Contains('.') && char.IsUpper(part.LastOrDefault()))
                        return part;
                }
            }

            return null;
        }

        private static Type? FindType(string fullName)
        {
            if (string.IsNullOrEmpty(fullName)) return null;
            foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
            {
                try
                {
                    var t = asm.GetType(fullName, throwOnError: false);
                    if (t != null) return t;
                }
                catch
                {
                    // ignore and continue
                }
            }
            return null;
        }
    }
}
