using System;
using System.Collections.Generic;
using System.Windows;
using BestFlex.Shell.Pages;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace BestFlex.Shell.Navigation
{
    public static class RouteRegistry
    {
        private static IServiceProvider? _serviceProvider;
        private static readonly Dictionary<string, Func<UIElement>> _routes
            = new(StringComparer.OrdinalIgnoreCase)
            {
                ["app://core/dashboard"] = () => GetService<DashboardPage>(),
                ["app://sales/new"] = () => new NewSaleLocalPage(),
                ["app://sales/invoices"] = () => GetService<InvoicesPage>(),
            };

        public static void Initialize(IServiceProvider serviceProvider)
        {
            _serviceProvider = serviceProvider ?? throw new ArgumentNullException(nameof(serviceProvider));
        }

        private static T GetService<T>() where T : class
        {
            if (_serviceProvider == null)
                throw new InvalidOperationException("RouteRegistry not initialized. Call Initialize() first.");
            return _serviceProvider.GetRequiredService<T>();
        }

        public static bool TryResolve(string route, out UIElement page)
        {
            if (_routes.TryGetValue(route, out var factory))
            {
                try
                {
                    page = factory();
                    return true;
                }
                catch (Exception ex)
                {
                    // Log the exception and rethrow to let Navigator handle it with proper logging
                    var logger = _serviceProvider?.GetService<ILogger<object>>();
                    logger?.LogError(ex, "Failed to resolve route {Route}", route);
                    throw;
                }
            }

            page = null!;
            return false;
        }
    }
}
