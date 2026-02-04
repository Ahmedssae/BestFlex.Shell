using System;
using Microsoft.Extensions.DependencyInjection;
using BestFlex.Shell.Pages;
using BestFlex.Shell.ViewModels;

namespace BestFlex.Shell.Factories
{
    /// <summary>
    /// Explicit view factory - no routes, no strings, no fallbacks
    /// </summary>
    public class ViewFactory
    {
        private readonly IServiceProvider _serviceProvider;

        public ViewFactory(IServiceProvider serviceProvider)
        {
            _serviceProvider = serviceProvider ?? throw new ArgumentNullException(nameof(serviceProvider));
        }

        public object CreateDashboard()
        {
            var vm = _serviceProvider.GetRequiredService<DashboardViewModel>();
            return new DashboardPage(vm);
        }

        public object CreateNewSale()
        {
            // Use DI to resolve NewSalePage which will get NewSaleViewModel with session store
            return _serviceProvider.GetRequiredService<NewSalePage>();
        }

        public object CreateCustomers()
        {
            var vm = _serviceProvider.GetRequiredService<DashboardViewModel>();
            return new DashboardPage(vm); // TODO: Create CustomersPage
        }

        public object CreateProducts()
        {
            var vm = _serviceProvider.GetRequiredService<DashboardViewModel>();
            return new DashboardPage(vm); // TODO: Create ProductsPage
        }

        public object CreateInvoices()
        {
            var vm = _serviceProvider.GetRequiredService<InvoicesPageViewModel>();
            return new InvoicesPage(vm);
        }
    }
}
