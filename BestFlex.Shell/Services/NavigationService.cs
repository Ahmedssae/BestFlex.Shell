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
using Microsoft.Win32;

namespace BestFlex.Shell.Services
{
    public sealed class NavigationService : IShellNavigationService
    {
        private readonly IServiceProvider _sp;
        private readonly IAuditService _audit;
        private readonly IErrorService _error;
        private readonly IUserNotificationService _notification;
        private readonly ConcurrentDictionary<string, SemaphoreSlim> _navigationLocks = new();

        public NavigationService(IServiceProvider sp, IAuditService audit, IErrorService error, IUserNotificationService notification)
        {
            _sp = sp ?? throw new ArgumentNullException(nameof(sp));
            _audit = audit ?? throw new ArgumentNullException(nameof(audit));
            _error = error ?? throw new ArgumentNullException(nameof(error));
            _notification = notification ?? throw new ArgumentNullException(nameof(notification));
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
                _error.Handle(ex, "NavigationService.OpenQuickAddCustomer");
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
                    await _audit.LogNavigationAsync($"InvoiceDetails:{invoiceId}");
                    
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
                    _error.Handle(ex, "NavigationService.OpenInvoiceDetails");
                    _notification.ShowError("Failed to open invoice details.");
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
                // Preload synchronously (best-effort)
                wnd.PreloadAsync(name, DateTime.Today.AddDays(-90), DateTime.Today, includeAging: true).GetAwaiter().GetResult();
            }
            catch { /* best-effort */ }

            wnd.ShowDialog();
        }

        public void OpenNewSale()
        {
            try
            {
                _ = Task.Run(async () => await _audit.LogNavigationAsync("NewSale"));
                
                var currentApp = System.Windows.Application.Current;
                if (currentApp == null) return;
                var app = (App)currentApp;
                // Navigate to the New Sale Page via the registered navigator
                var nav = app.Services.GetService<BestFlex.Shell.Navigation.INavigator>();
                nav?.Navigate("app://sales/new");
            }
            catch (Exception ex)
            {
                _error.Handle(ex, "NavigationService.OpenNewSale");
                _notification.ShowError("Failed to open new sale.");
            }
        }

        public void OpenLowStock(int threshold)
        {
            try
            {
                _ = Task.Run(async () => await _audit.LogNavigationAsync($"LowStock:{threshold}"));
                
                var currentApp = System.Windows.Application.Current;
                if (currentApp == null) return;
                var app = (App)currentApp;
                var wnd = app.Services.GetService<Windows.LowStockWindow>() ?? ActivatorUtilities.CreateInstance<Windows.LowStockWindow>(_sp, new object[] { threshold });
                wnd.Owner = System.Windows.Application.Current?.MainWindow;
                wnd.ShowDialog();
            }
            catch (Exception ex)
            {
                _error.Handle(ex, "NavigationService.OpenLowStock");
                _notification.ShowError("Failed to open low stock window.");
            }
        }

        public void OpenUnpaidInvoices(int topN, int? preselectCustomerId = null)
        {
            try
            {
                _ = Task.Run(async () => await _audit.LogNavigationAsync($"UnpaidInvoices:{topN}{(preselectCustomerId.HasValue ? $":{preselectCustomerId}" : "")}"));
                
                var currentApp = System.Windows.Application.Current;
                if (currentApp == null) return;
                var app = (App)currentApp;
                var wnd = app.Services.GetService<Windows.UnpaidInvoicesWindow>() ?? ActivatorUtilities.CreateInstance<Windows.UnpaidInvoicesWindow>(_sp, new object[] { topN, preselectCustomerId ?? (object?)null });
                wnd.Owner = System.Windows.Application.Current?.MainWindow;
                wnd.ShowDialog();
            }
            catch (Exception ex)
            {
                _error.Handle(ex, "NavigationService.OpenUnpaidInvoices");
                _notification.ShowError("Failed to open unpaid invoices.");
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

