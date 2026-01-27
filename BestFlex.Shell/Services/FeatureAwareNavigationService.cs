using System;
using System.Threading.Tasks;
using System.Windows;
using BestFlex.Application.Abstractions;
using BestFlex.Shell.Abstractions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace BestFlex.Shell.Services
{
    /// <summary>
    /// Feature-aware navigation service that checks feature availability before navigation
    /// </summary>
    public class FeatureAwareNavigationService : IShellNavigationService, INavigationService
    {
        private readonly IServiceProvider _sp;
        private readonly IFeatureService _featureService;
        private readonly IAuditService _audit;
        private readonly IErrorService _error;
        private readonly IUserNotificationService _notification;
        private readonly ILogger<FeatureAwareNavigationService> _logger;
        private readonly IPrintingAvailabilityService _printingAvailability;

        public FeatureAwareNavigationService(
            IServiceProvider sp,
            IFeatureService featureService,
            IAuditService audit,
            IErrorService error,
            IUserNotificationService notification,
            ILogger<FeatureAwareNavigationService> logger,
            IPrintingAvailabilityService printingAvailability)
        {
            _sp = sp ?? throw new ArgumentNullException(nameof(sp));
            _featureService = featureService ?? throw new ArgumentNullException(nameof(featureService));
            _audit = audit ?? throw new ArgumentNullException(nameof(audit));
            _error = error ?? throw new ArgumentNullException(nameof(error));
            _notification = notification ?? throw new ArgumentNullException(nameof(notification));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _printingAvailability = printingAvailability ?? throw new ArgumentNullException(nameof(printingAvailability));
        }

        public void NavigateToDashboard()
        {
            NavigateWithFeatureCheck(
                route: NavigationRoutes.Dashboard,
                feature: "Navigation",
                action: () =>
                {
                    var navigator = _sp.GetService<BestFlex.Shell.Navigation.INavigator>();
                    if (navigator != null && !navigator.NavigateSafe("dashboard", "Failed to load dashboard"))
                    {
                        _notification.ShowError("Dashboard page unavailable");
                    }
                });
        }

        public void OpenQuickAddCustomer(Window? owner = null)
        {
            NavigateWithFeatureCheck(
                route: NavigationRoutes.Customers,
                feature: "CustomerLookup",
                action: () =>
                {
                    var window = _sp.GetService<BestFlex.Shell.Windows.QuickAddCustomerWindow>() 
                        ?? ActivatorUtilities.CreateInstance<BestFlex.Shell.Windows.QuickAddCustomerWindow>(_sp, Array.Empty<object>());
                    if (window != null)
                    {
                        window.Owner = owner ?? System.Windows.Application.Current?.MainWindow;
                        window.ShowDialog();
                    }
                },
                owner: owner,
                context: "Quick Add Customer"
            );
        }

        public void OpenQuickAddProduct(Window? owner = null)
        {
            NavigateWithFeatureCheck(
                route: NavigationRoutes.Products,
                feature: "ProductLookup",
                action: () =>
                {
                    var window = _sp.GetService<BestFlex.Shell.Windows.QuickAddProductWindow>()
                        ?? ActivatorUtilities.CreateInstance<BestFlex.Shell.Windows.QuickAddProductWindow>(_sp, Array.Empty<object>());
                    if (window != null)
                    {
                        window.Owner = owner ?? System.Windows.Application.Current?.MainWindow;
                        window.ShowDialog();
                    }
                },
                owner: owner,
                context: "Quick Add Product"
            );
        }

        public void OpenGrnPreview(object document, Window? owner = null)
        {
            NavigateWithFeatureCheck(
                route: NavigationRoutes.GrnPreview,
                feature: "Printing",
                action: () =>
                {
                    if (!_printingAvailability.IsPrintingAvailable)
                    {
                        _notification.ShowWarning(_printingAvailability.GetPrintingUnavailableReason() ?? "Printing is not available");
                        return;
                    }
                    
                    // Use GrnPreviewWindow for GRN preview
                    var window = _sp.GetService<BestFlex.Shell.Windows.GrnPreviewWindow>()
                        ?? ActivatorUtilities.CreateInstance<BestFlex.Shell.Windows.GrnPreviewWindow>(_sp, new object[] { document });
                    if (window != null)
                    {
                        window.Owner = owner ?? System.Windows.Application.Current?.MainWindow;
                        window.ShowDialog();
                    }
                },
                owner: owner,
                context: "GRN Preview"
            );
        }

        public void OpenPrintPreview(object document, Window? owner = null)
        {
            NavigateWithFeatureCheck(
                route: NavigationRoutes.Invoices,
                feature: "Printing",
                action: () =>
                {
                    if (!_printingAvailability.IsPrintingAvailable)
                    {
                        _notification.ShowWarning(_printingAvailability.GetPrintingUnavailableReason() ?? "Printing is not available");
                        return;
                    }
                    
                    // Use InvoicePreviewWindow for print preview
                    var window = _sp.GetService<BestFlex.Shell.Windows.InvoicePreviewWindow>()
                        ?? ActivatorUtilities.CreateInstance<BestFlex.Shell.Windows.InvoicePreviewWindow>(_sp, new object[] { document });
                    if (window != null)
                    {
                        window.Owner = owner ?? System.Windows.Application.Current?.MainWindow;
                        window.ShowDialog();
                    }
                },
                owner: owner,
                context: "Print Preview"
            );
        }

        public void ShowPrintDialog()
        {
            NavigateWithFeatureCheck(
                route: NavigationRoutes.Invoices,
                feature: "Printing",
                action: () =>
                {
                    var dialog = new System.Windows.Controls.PrintDialog();
                    dialog.ShowDialog();
                },
                context: "Print Dialog"
            );
        }

        public void ShowSaveFileDialog(string defaultName, string filter, Action<string>? onFileSelected = null)
        {
            var dialog = new Microsoft.Win32.SaveFileDialog
            {
                FileName = defaultName,
                Filter = filter
            };

            if (dialog.ShowDialog() == true)
            {
                onFileSelected?.Invoke(dialog.FileName);
            }
        }

        public void ShowOpenFileDialog(string filter, Action<string>? onFileSelected = null)
        {
            var dialog = new Microsoft.Win32.OpenFileDialog { Filter = filter };

            if (dialog.ShowDialog() == true)
            {
                onFileSelected?.Invoke(dialog.FileName);
            }
        }

        public void ShowMessageBox(string message, string title, MessageBoxButton button, MessageBoxImage icon, Window? owner = null)
        {
            MessageBox.Show(owner ?? System.Windows.Application.Current?.MainWindow, message, title, button, icon);
        }

        // INavigationService implementation
        public void OpenInvoiceDetails(int invoiceId)
        {
            NavigateWithFeatureCheck(
                route: NavigationRoutes.Invoices,
                feature: "Sales",
                action: () =>
                {
                    // For now, just show a message - InvoiceDetailsWindow doesn't exist yet
                    _notification.ShowInfo($"Invoice #{invoiceId} details would be shown here.");
                },
                context: $"Invoice Details #{invoiceId}"
            );
        }

        public void OpenAccountStatement(int customerId)
        {
            NavigateWithFeatureCheck(
                route: NavigationRoutes.AccountStatement,
                feature: "CustomerLookup",
                action: () =>
                {
                    var window = _sp.GetService<BestFlex.Shell.Windows.AccountStatementWindow>()
                        ?? ActivatorUtilities.CreateInstance<BestFlex.Shell.Windows.AccountStatementWindow>(_sp, new object[] { customerId });
                    if (window != null)
                    {
                        window.Owner = System.Windows.Application.Current?.MainWindow;
                        window.ShowDialog();
                    }
                },
                context: $"Account Statement for Customer #{customerId}"
            );
        }

        public void OpenNewSale()
        {
            NavigateWithFeatureCheck(
                route: NavigationRoutes.NewSale,
                feature: "Sales",
                action: () =>
                {
                    var navigationService = _sp.GetService<BestFlex.Shell.Abstractions.IShellNavigationService>();
                    navigationService?.NavigateToDashboard();
                },
                context: "New Sale"
            );
        }

        public void OpenLowStock(int threshold)
        {
            NavigateWithFeatureCheck(
                route: NavigationRoutes.LowStock,
                feature: "ProductLookup",
                action: () =>
                {
                    var window = _sp.GetService<BestFlex.Shell.Windows.LowStockWindow>()
                        ?? ActivatorUtilities.CreateInstance<BestFlex.Shell.Windows.LowStockWindow>(_sp, new object[] { threshold });
                    if (window != null)
                    {
                        window.Owner = System.Windows.Application.Current?.MainWindow;
                        window.ShowDialog();
                    }
                },
                context: $"Low Stock (threshold: {threshold})"
            );
        }

        public void OpenUnpaidInvoices(int topN, int? preselectCustomerId = null)
        {
            NavigateWithFeatureCheck(
                route: NavigationRoutes.UnpaidInvoices,
                feature: "Sales",
                action: () =>
                {
                    var window = _sp.GetService<BestFlex.Shell.Windows.UnpaidInvoicesWindow>()
                        ?? ActivatorUtilities.CreateInstance<BestFlex.Shell.Windows.UnpaidInvoicesWindow>(_sp, new object[] { topN, preselectCustomerId! });
                    if (window != null)
                    {
                        window.Owner = System.Windows.Application.Current?.MainWindow;
                        window.ShowDialog();
                    }
                },
                context: $"Unpaid Invoices (Top {topN})"
            );
        }

        private void NavigateWithFeatureCheck(string route, string feature, Action action, Window? owner = null, string? context = null)
        {
            try
            {
                _logger.LogInformation("Navigation requested: {Route} (Context: {Context})", route, context ?? route);

                // Check if required features are available
                var requiredFeatures = GetRequiredFeaturesForRoute(route);
                foreach (var requiredFeature in requiredFeatures)
                {
                    if (!_featureService.IsFeatureAvailable(requiredFeature))
                    {
                        var reason = _featureService.GetFeatureUnavailableReason(requiredFeature) 
                            ?? $"{requiredFeature} feature not available";
                        
                        _logger.LogWarning("Navigation blocked: {Route} - {Feature} unavailable: {Reason}", route, requiredFeature, reason);
                        _notification.ShowWarning($"Cannot open {context ?? route}: {reason}");
                        return;
                    }
                }

                // All features available, proceed with navigation
                _logger.LogInformation("Navigation allowed: {Route} - all required features available", route);
                action();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Navigation failed: {Route}", route);
                _error.Handle(ex, $"Failed to open {context ?? route}");
                _notification.ShowError($"Failed to open {context ?? route}. Please try again.");
            }
        }

        private string[] GetRequiredFeaturesForRoute(string route)
        {
            return route switch
            {
                NavigationRoutes.Dashboard => NavigationRoutes.FeatureRequirements.Dashboard,
                NavigationRoutes.NewSale => NavigationRoutes.FeatureRequirements.NewSale,
                NavigationRoutes.Invoices => NavigationRoutes.FeatureRequirements.Invoices,
                NavigationRoutes.CustomerStatements => NavigationRoutes.FeatureRequirements.CustomerStatements,
                NavigationRoutes.Products => NavigationRoutes.FeatureRequirements.Products,
                NavigationRoutes.Reports => NavigationRoutes.FeatureRequirements.Reports,
                NavigationRoutes.Settings => NavigationRoutes.FeatureRequirements.Settings,
                NavigationRoutes.TemplateDesigner => NavigationRoutes.FeatureRequirements.TemplateDesigner,
                NavigationRoutes.LowStock => NavigationRoutes.FeatureRequirements.LowStock,
                NavigationRoutes.UnpaidInvoices => NavigationRoutes.FeatureRequirements.UnpaidInvoices,
                NavigationRoutes.AccountStatement => NavigationRoutes.FeatureRequirements.AccountStatement,
                NavigationRoutes.ReceiveStock => NavigationRoutes.FeatureRequirements.ReceiveStock,
                NavigationRoutes.GrnPreview => NavigationRoutes.FeatureRequirements.GrnPreview,
                _ => Array.Empty<string>()
            };
        }
    }
}
