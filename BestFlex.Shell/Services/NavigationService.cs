using System;
using BestFlex.Application.Abstractions;
using Microsoft.Extensions.DependencyInjection;

namespace BestFlex.Shell.Services
{
    public class NavigationService : INavigationService
    {
        private readonly IServiceProvider _sp;
        public NavigationService(IServiceProvider sp) => _sp = sp ?? throw new ArgumentNullException(nameof(sp));

        public void OpenInvoiceDetails(int invoiceId)
        {
            var app = (App)System.Windows.Application.Current!;
            var wnd = app.Services.GetService<InvoiceDetailsWindow>() ?? ActivatorUtilities.CreateInstance<InvoiceDetailsWindow>(_sp, Array.Empty<object>());
            wnd.InvoiceId = invoiceId;
            wnd.Owner = System.Windows.Application.Current?.MainWindow;
            wnd.ShowDialog();
        }

        public void OpenAccountStatement(int customerId)
        {
            var app = (App)System.Windows.Application.Current!;
            var wnd = app.Services.GetService<Windows.AccountStatementWindow>() ?? ActivatorUtilities.CreateInstance<Windows.AccountStatementWindow>(_sp, Array.Empty<object>());
            wnd.Owner = System.Windows.Application.Current?.MainWindow;

            try
            {
                using var scope = _sp.CreateScope();
                var db = scope.ServiceProvider.GetService<BestFlex.Persistence.Data.BestFlexDbContext>();
                var name = string.Empty;
                if (db != null)
                {
                    var cust = db.CustomerAccounts.Find(customerId);
                    name = cust?.Name ?? string.Empty;
                }
                // Preload synchronously (best-effort)
                wnd.PreloadAsync(name, DateTime.Today.AddDays(-90), DateTime.Today, includeAging: true).GetAwaiter().GetResult();
            }
            catch { /* best-effort */ }

            wnd.ShowDialog();
        }

        public void OpenNewSale()
        {
            var app = (App)System.Windows.Application.Current!;
            // Navigate to the New Sale Page via the registered navigator
            var nav = app.Services.GetService<BestFlex.Shell.Navigation.INavigator>();
            try
            {
                nav?.Navigate("app://sales/new");
            }
            catch { }
        }

        public void OpenLowStock(int threshold)
        {
            var app = (App)System.Windows.Application.Current!;
            var wnd = app.Services.GetService<Windows.LowStockWindow>() ?? ActivatorUtilities.CreateInstance<Windows.LowStockWindow>(_sp, new object[] { threshold });
            wnd.Owner = System.Windows.Application.Current?.MainWindow;
            wnd.ShowDialog();
        }

        public void OpenUnpaidInvoices(int topN, int? preselectCustomerId = null)
        {
            var app = (App)System.Windows.Application.Current!;
            var wnd = app.Services.GetService<Windows.UnpaidInvoicesWindow>() ?? ActivatorUtilities.CreateInstance<Windows.UnpaidInvoicesWindow>(_sp, new object[] { topN, preselectCustomerId ?? (object?)null });
            wnd.Owner = System.Windows.Application.Current?.MainWindow;
            wnd.ShowDialog();
        }
    }
}

