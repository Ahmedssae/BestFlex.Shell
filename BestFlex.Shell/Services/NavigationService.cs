using System;
using System.Collections.Concurrent;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using BestFlex.Application.Abstractions;
using BestFlex.Shell.Abstractions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Win32;

namespace BestFlex.Shell.Services
{
    public sealed class NavigationService : IShellNavigationService
    {
        private readonly IServiceProvider _sp;
        private readonly IAuditService _audit;
        private readonly IErrorService _error;
        private readonly IUserNotificationService _notification;
        private readonly BestFlex.Application.Abstractions.ISalesModuleGate _salesGate;
        private readonly ConcurrentDictionary<string, SemaphoreSlim> _navigationLocks = new();

        public NavigationService(IServiceProvider sp, IAuditService audit, IErrorService error, IUserNotificationService notification, BestFlex.Application.Abstractions.ISalesModuleGate salesGate)
        {
            _sp = sp ?? throw new ArgumentNullException(nameof(sp));
            _audit = audit ?? throw new ArgumentNullException(nameof(audit));
            _error = error ?? throw new ArgumentNullException(nameof(error));
            _notification = notification ?? throw new ArgumentNullException(nameof(notification));
            _salesGate = salesGate ?? throw new ArgumentNullException(nameof(salesGate));
        }
        
        public void NavigateToDashboard()
        {
            try
            {
                var mainWindow = System.Windows.Application.Current?.MainWindow;
                if (mainWindow == null)
                {
                    _notification.ShowError("Main window not available");
                    return;
                }

                var navigator = _sp.GetService<BestFlex.Shell.Navigation.INavigator>();
                if (navigator == null)
                {
                    _notification.ShowError("Navigation service not available");
                    return;
                }

                // Dashboard is ALWAYS available - never gated, never optional
                // If navigation fails, create dashboard directly as ultimate fallback
                if (!navigator.NavigateSafe("dashboard", "Failed to load dashboard page"))
                {
                    var logger = _sp.GetService<Microsoft.Extensions.Logging.ILogger<NavigationService>>();
                    logger?.LogWarning("Dashboard navigation failed, creating dashboard directly");
                    
                    // Ultimate fallback - create dashboard directly without navigator
                    try
                    {
                        var vm = new BestFlex.Shell.ViewModels.DashboardViewModel();
                        var dashboardPage = new BestFlex.Shell.Pages.DashboardPage(vm);
                        ((MainWindow)mainWindow).MainHost.Content = dashboardPage;
                    }
                    catch (Exception fallbackEx)
                    {
                        logger?.LogError(fallbackEx, "Even direct dashboard creation failed");
                        // Last resort - show safe fallback
                        var fallbackPage = new BestFlex.Shell.Pages.SafeFallbackPage("Dashboard temporarily unavailable");
                        ((MainWindow)mainWindow).MainHost.Content = fallbackPage;
                    }
                }
                else
                {
                    // Set the loaded page to the MainHost
                    ((MainWindow)mainWindow).MainHost.Content = navigator.Current;
                }
            }
            catch (Exception ex)
            {
                var unwrapped = ReflectionExceptionUnwrapper.Unwrap(ex);
                _error.Handle(unwrapped, "NavigationService.NavigateToDashboard");
                
                // Even in case of exception, never leave user stuck - show dashboard fallback
                try
                {
                    var mainWindow = System.Windows.Application.Current?.MainWindow;
                    if (mainWindow is MainWindow mainWin && mainWin.MainHost != null)
                    {
                        var fallbackPage = new BestFlex.Shell.Pages.SafeFallbackPage("Dashboard temporarily unavailable");
                        mainWin.MainHost.Content = fallbackPage;
                    }
                }
                catch
                {
                    // Last resort - at least don't crash
                    _notification.ShowError("Dashboard unavailable but application continues");
                }
            }
        }
        
        public void OpenQuickAddCustomer(Window? owner = null)
        {
            try
            {
                var window = _sp.GetService<BestFlex.Shell.Windows.QuickAddCustomerWindow>() ?? ActivatorUtilities.CreateInstance<BestFlex.Shell.Windows.QuickAddCustomerWindow>(_sp, Array.Empty<object>());
                if (window != null)
                {
                    window.Owner = owner ?? System.Windows.Application.Current?.MainWindow;
                    window.ShowDialog();
                }
            }
            catch (Exception ex)
            {
                var unwrapped = ReflectionExceptionUnwrapper.Unwrap(ex);
                _error.Handle(unwrapped, "NavigationService.OpenQuickAddCustomer");
            }
        }

        public void OpenInvoiceDetails(int invoiceId)
        {
            var key = $"InvoiceDetails_{invoiceId}";
            var lockObj = _navigationLocks.GetOrAdd(key, _ => new SemaphoreSlim(1, 1));
            
            Task.Run(async () => 
            {
                await lockObj.WaitAsync();
                try
                {
                    // Check ERP v1.0 capability constraints
                    if (!_salesGate.IsInvoicePostingEnabled())
                    {
                        _notification.ShowError("Invoice details are not available in this version of BestFlex ERP.");
                        return;
                    }

                    var currentApp = System.Windows.Application.Current;
                    if (currentApp == null) return;
                    var app = (App)currentApp;
                    var wnd = app.Services.GetService<InvoiceDetailsWindow>() ?? ActivatorUtilities.CreateInstance<InvoiceDetailsWindow>(_sp, Array.Empty<object>());
                    wnd.InvoiceId = invoiceId;
                    wnd.Owner = System.Windows.Application.Current?.MainWindow;
                    wnd.ShowDialog();
                }
                catch (Exception ex)
                {
                    var unwrapped = ReflectionExceptionUnwrapper.Unwrap(ex);
                    _error.Handle(unwrapped, "NavigationService.OpenInvoiceDetails");
                    _notification.ShowError(ReflectionExceptionUnwrapper.GetUserFriendlyMessage(unwrapped));
                }
                finally
                {
                    lockObj.Release();
                    // Clean up lock after a delay to prevent memory leak
                    _ = Task.Delay(TimeSpan.FromSeconds(5)).ContinueWith(_ => 
                    {
                        _navigationLocks.TryRemove(key, out var _lock);
                        _lock?.Dispose();
                    });
                }
            });
        }

        public void OpenAccountStatement(int customerId)
        {
                var currentApp = System.Windows.Application.Current;
                if (currentApp == null) return;
                var app = (App)currentApp;
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
                // Preload asynchronously and non-blocking (best-effort)
                _ = Task.Run(async () =>
                {
                    try
                    {
                        await wnd.PreloadAsync(name, DateTime.Today.AddDays(-90), DateTime.Today, includeAging: true);
                    }
                    catch
                    {
                        // Ignore preload failures - window will still open
                    }
                });
            }
            catch { /* best-effort */ }

            wnd.ShowDialog();
        }

        public void OpenNewSale()
        {
            try
            {
                // EXPLICIT NAVIGATION: No routes, no strings, no indirection
                var currentApp = System.Windows.Application.Current;
                if (currentApp == null) return;
                var app = (App)currentApp;
                
                var contentHost = app.Services.GetRequiredService<BestFlex.Shell.Abstractions.IMainContentHost>();
                var viewFactory = app.Services.GetRequiredService<BestFlex.Shell.Factories.ViewFactory>();
                
                // DETERMINISTIC: Direct factory call
                contentHost.Show(viewFactory.CreateNewSale());
            }
            catch (Exception ex)
            {
                var unwrapped = ReflectionExceptionUnwrapper.Unwrap(ex);
                _error.Handle(unwrapped, "NavigationService.OpenNewSale");
                _notification.ShowError(ReflectionExceptionUnwrapper.GetUserFriendlyMessage(unwrapped));
            }
        }

        public void OpenLowStock(int threshold = 5)
        {
            try
            {
                var currentApp = System.Windows.Application.Current;
                if (currentApp == null) return;
                var app = (App)currentApp;
                var wnd = app.Services.GetService<Windows.LowStockWindow>() ?? ActivatorUtilities.CreateInstance<Windows.LowStockWindow>(_sp, new object[] { threshold });
                wnd.Owner = System.Windows.Application.Current?.MainWindow;
                wnd.ShowDialog();
            }
            catch (Exception ex)
            {
                var unwrapped = ReflectionExceptionUnwrapper.Unwrap(ex);
                _error.Handle(unwrapped, "NavigationService.OpenLowStock");
                _notification.ShowError(ReflectionExceptionUnwrapper.GetUserFriendlyMessage(unwrapped));
            }
        }

        public void OpenUnpaidInvoices(int topN, int? preselectCustomerId = null)
        {
            try
            {
                var currentApp = System.Windows.Application.Current;
                if (currentApp == null) return;
                var app = (App)currentApp;
                var wnd = app.Services.GetService<Windows.UnpaidInvoicesWindow>() ?? ActivatorUtilities.CreateInstance<Windows.UnpaidInvoicesWindow>(_sp, new object[] { topN, preselectCustomerId ?? (object?)null! });
                wnd.Owner = System.Windows.Application.Current?.MainWindow;
                wnd.ShowDialog();
            }
            catch (Exception ex)
            {
                var unwrapped = ReflectionExceptionUnwrapper.Unwrap(ex);
                _error.Handle(unwrapped, "[NavigationService] OpenUnpaidInvoices");
                _notification.ShowError(ReflectionExceptionUnwrapper.GetUserFriendlyMessage(unwrapped));
            }
        }
        
        public void OpenQuickAddProduct(Window? owner = null)
        {
            var currentApp = System.Windows.Application.Current;
            if (currentApp == null) return;
            var app = (App)currentApp;
            var wnd = app.Services.GetService<Windows.QuickAddProductWindow>() ?? ActivatorUtilities.CreateInstance<Windows.QuickAddProductWindow>(_sp, Array.Empty<object>());
            wnd.Owner = owner ?? System.Windows.Application.Current?.MainWindow;
            wnd.ShowDialog();
        }
        
        public void OpenGrnPreview(object document, Window? owner = null)
        {
            var currentApp = System.Windows.Application.Current;
            if (currentApp == null) return;
            var app = (App)currentApp;
            var wnd = app.Services.GetService<Windows.GrnPreviewWindow>() ?? ActivatorUtilities.CreateInstance<Windows.GrnPreviewWindow>(_sp, Array.Empty<object>());
            wnd.Owner = owner ?? System.Windows.Application.Current?.MainWindow;
                if (wnd is Windows.GrnPreviewWindow grnWnd && document is System.Windows.Documents.FlowDocument fd)
                {
                    grnWnd.SetDocument(fd);
                }
            wnd.ShowDialog();
        }
        
        public void OpenPrintPreview(object document, Window? owner = null)
        {
            var currentApp = System.Windows.Application.Current;
            if (currentApp == null) return;
            var app = (App)currentApp;
            var wnd = app.Services.GetService<PrintPreviewWindow>() ?? ActivatorUtilities.CreateInstance<PrintPreviewWindow>(_sp, Array.Empty<object>());
            wnd.Owner = owner ?? System.Windows.Application.Current?.MainWindow;
            if (wnd is PrintPreviewWindow previewWnd && document is System.Windows.Documents.FlowDocument fd2)
            {
                previewWnd.Load(fd2);
            }
            wnd.ShowDialog();
        }
        
        public void ShowPrintDialog()
        {
            var pd = new PrintDialog();
            pd.ShowDialog();
        }
        
        public void ShowSaveFileDialog(string defaultName, string filter, Action<string>? onFileSelected = null)
        {
            var sfd = new SaveFileDialog
            {
                FileName = defaultName,
                Filter = filter,
                InitialDirectory = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments)
            };
            if (sfd.ShowDialog() == true)
            {
                onFileSelected?.Invoke(sfd.FileName);
            }
        }
        
        public void ShowOpenFileDialog(string filter, Action<string>? onFileSelected = null)
        {
            var ofd = new OpenFileDialog
            {
                Filter = filter,
                InitialDirectory = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments)
            };
            if (ofd.ShowDialog() == true)
            {
                onFileSelected?.Invoke(ofd.FileName);
            }
        }
        
        public void ShowMessageBox(string message, string title, MessageBoxButton button, MessageBoxImage icon, Window? owner = null)
        {
            MessageBox.Show(owner ?? System.Windows.Application.Current?.MainWindow, message, title, button, icon);
        }
    }
}

