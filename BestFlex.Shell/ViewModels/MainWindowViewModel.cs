using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Input;
using BestFlex.Application.Abstractions;
using BestFlex.Infrastructure.Services;
using BestFlex.Persistence.Data;
using BestFlex.Shell.Services;
using BestFlex.Shell.Printing;
using BestFlex.Shell.Abstractions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace BestFlex.Shell.ViewModels
{
    public sealed class MainWindowViewModel : ViewModelBase
    {
        private readonly IPermissionService _permissions;
        private readonly IServiceProvider _services;
        private readonly IErrorService _error;
        private readonly IUserNotificationService _notification;
        private readonly IFeatureService _featureService;
        private readonly ILogger<MainWindowViewModel> _logger;
        private readonly IPrintingAvailabilityService _printingAvailability;
        private bool _isAdmin;
        private bool _isBusy;

        public MainWindowViewModel(
            IServiceProvider services,
            ILogger<MainWindowViewModel> logger,
            IFeatureService featureService,
            IPermissionService permissions,
            IErrorService error,
            IUserNotificationService notification,
            IPrintingAvailabilityService printingAvailability)
        {
            _services = services ?? throw new ArgumentNullException(nameof(services));
            _featureService = featureService ?? throw new ArgumentNullException(nameof(featureService));
            _permissions = permissions ?? throw new ArgumentNullException(nameof(permissions));
            _error = error ?? throw new ArgumentNullException(nameof(error));
            _notification = notification ?? throw new ArgumentNullException(nameof(notification));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _printingAvailability = printingAvailability ?? throw new ArgumentNullException(nameof(printingAvailability));

            // Initialize commands
            ReprintLastInvoiceCommand = new AsyncRelayCommand(ReprintLastInvoiceAsync, CanReprintLastInvoice);
            ChangePasswordCommand = new AsyncRelayCommand(ChangePasswordAsync, CanChangePassword);
            SignOutCommand = new AsyncRelayCommand(SignOutAsync, CanSignOut);
            OpenSettingsCommand = new AsyncRelayCommand(OpenSettingsAsync, CanOpenSettings);
        }

        // Permission properties for menu binding
        public bool CanManageSettings => _permissions.CanManageSettings();
        public bool IsAdmin => _isAdmin;

        public bool IsBusy
        {
            get => _isBusy;
            private set
            {
                if (SetProperty(ref _isBusy, value))
                {
                    // Update command states when IsBusy changes
                    ReprintLastInvoiceCommand?.RaiseCanExecuteChanged();
                    ChangePasswordCommand?.RaiseCanExecuteChanged();
                    SignOutCommand?.RaiseCanExecuteChanged();
                    OpenSettingsCommand?.RaiseCanExecuteChanged();
                }
            }
        }

        // Public feature access methods for MainWindow code-behind
        public bool IsFeatureAvailable(string featureName) => _featureService.IsFeatureAvailable(featureName);
        public string? GetFeatureUnavailableReason(string featureName) => _featureService.GetFeatureUnavailableReason(featureName);
        public bool HasUnavailableCoreFeatures() => _featureService.HasUnavailableCoreFeatures();
        public IEnumerable<FeatureDefinition> GetUnavailableCoreFeatures() => _featureService.GetUnavailableCoreFeatures();

        public AsyncRelayCommand ReprintLastInvoiceCommand { get; }
        public AsyncRelayCommand ChangePasswordCommand { get; }
        public AsyncRelayCommand SignOutCommand { get; }
        public AsyncRelayCommand OpenSettingsCommand { get; }

        public async Task LoadAsync()
        {
            try
            {
                _logger.LogInformation("MainWindowViewModel.LoadAsync started");
                
                // Check for unavailable CORE features only - this is the only place we should ever block startup
                var unavailableCoreFeatures = _featureService.GetUnavailableCoreFeatures();
                if (unavailableCoreFeatures.Any())
                {
                    var featureNames = string.Join(", ", unavailableCoreFeatures.Select(f => f.Name));
                    var message = $"Core ERP features unavailable: {featureNames}. Application cannot continue.";
                    _logger.LogCritical("Core features unavailable: {FeatureNames}", featureNames);
                    throw new InvalidOperationException(message);
                }

                // Detect admin role
                _isAdmin = DetectIsAdmin();
                
                // Update command states
                ReprintLastInvoiceCommand.RaiseCanExecuteChanged();
                ChangePasswordCommand.RaiseCanExecuteChanged();
                SignOutCommand.RaiseCanExecuteChanged();
                OpenSettingsCommand.RaiseCanExecuteChanged();
                
                OnPropertyChanged(nameof(CanManageSettings));
                
                _logger.LogInformation("MainWindowViewModel.LoadAsync completed successfully");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "MainWindowViewModel.LoadAsync failed");
                _error.Handle(ex, "Failed to load main window");
                throw;
            }
        }

        public async Task InitializeAfterLoginAsync()
        {
            try
            {
                _logger.LogInformation("InitializeAfterLoginAsync started");
                
                // Validate Navigation CORE feature
                if (!_featureService.IsFeatureAvailable("Navigation"))
                {
                    var reason = _featureService.GetFeatureUnavailableReason("Navigation") ?? "Navigation feature not available";
                    throw new InvalidOperationException($"Navigation core feature unavailable: {reason}");
                }

                // Navigate to Dashboard
                var navigationService = _services.GetRequiredService<INavigationService>();
                navigationService.OpenNewSale(); // Navigate to a safe default page
                
                _logger.LogInformation("InitializeAfterLoginAsync completed successfully");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "InitializeAfterLoginAsync failed");
                
                // Show visible error message
                var errorMessage = $"The application could not load the dashboard: {ex.Message}";
                _notification.ShowError(errorMessage);
                
                // Navigate to SafeFallbackView
                try
                {
                    var navigationService = _services.GetRequiredService<INavigationService>();
                    navigationService.OpenNewSale(); // Fallback to new sale page
                }
                catch (Exception fallbackEx)
                {
                    _logger.LogCritical(fallbackEx, "Failed to navigate to fallback page");
                    _notification.ShowError("Critical error: Application cannot recover.");
                }
            }
        }

        private async Task ReprintLastInvoiceAsync()
        {
            try
            {
                _logger.LogInformation("ReprintLastInvoiceAsync started");
                
                // Check if printing is available (optional feature)
                if (!_featureService.IsFeatureAvailable("Printing"))
                {
                    var reason = _featureService.GetFeatureUnavailableReason("Printing") ?? "Printing module not available";
                    _notification.ShowWarning($"Printing not available: {reason}");
                    _logger.LogWarning("Printing feature unavailable: {Reason}", reason);
                    return;
                }

                var tracker = _services.GetRequiredService<ILastInvoiceTracker>();
                if (tracker.LastInvoiceId == null)
                {
                    _notification.ShowInfo("No invoice created in this session yet.");
                    return;
                }

                var db = _services.GetRequiredService<BestFlexDbContext>();
                var tplProvider = _services.GetRequiredService<IInvoiceTemplateProvider>();
                var printEngine = _services.GetRequiredService<IInvoicePrintEngine>();

                var inv = await db.SellingInvoices
                    .Include(i => i.CustomerAccount)
                    .Include(i => i.SellingInvoiceItems).ThenInclude(it => it.Product)
                    .FirstOrDefaultAsync(i => i.Id == tracker.LastInvoiceId.Value);

                if (inv == null)
                {
                    _notification.ShowWarning("Last invoice not found.");
                    return;
                }

                var draft = new BestFlex.Shell.Models.SaleDraft
                {
                    InvoiceNumber = inv.InvoiceNo,
                    InvoiceDate = inv.IssuedAt,
                    CustomerName = inv.CustomerAccount.Name,
                    Currency = inv.Currency,
                    Subtotal = inv.SellingInvoiceItems.Sum(x => x.UnitPrice * x.Quantity),
                    DiscountPercent = 0m,
                    TaxPercent = 0m,
                    GrandTotal = inv.SellingInvoiceItems.Sum(x => x.UnitPrice * x.Quantity)
                };
                
                foreach (var it in inv.SellingInvoiceItems)
                {
                    draft.Lines.Add(new BestFlex.Shell.Models.SaleDraftLine
                    {
                        ProductId = it.ProductId,
                        Code = it.Product.Code,
                        Name = it.Product.Name,
                        Qty = it.Quantity,
                        Price = it.UnitPrice
                    });
                }

                var company = await db.Companies.AsNoTracking().OrderBy(c => c.Id).FirstOrDefaultAsync();
                var ctx = new CompanyPrintContext
                {
                    CompanyId = company?.Id ?? 1,
                    CompanyName = company?.Name ?? "Company"
                };

                var tpl = tplProvider.GetTemplateForCompany(ctx.CompanyId);
                var doc = printEngine.Render(draft, tpl, ctx);

                var wnd = new QuickPrintPreviewWindow
                {
                    Owner = System.Windows.Application.Current?.MainWindow
                };
                wnd.SetDocument(doc);
                wnd.ShowDialog();
                
                _logger.LogInformation("ReprintLastInvoiceAsync completed successfully");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "ReprintLastInvoiceAsync failed");
                _error.Handle(ex, "Failed to reprint last invoice");
            }
        }

        private bool CanReprintLastInvoice() => !IsBusy && _printingAvailability.IsPrintingAvailable;

        private async Task ChangePasswordAsync()
        {
            try
            {
                _logger.LogInformation("ChangePasswordAsync started");
                
                var app = (App)System.Windows.Application.Current;
                var window = app.Services.GetRequiredService<ChangePasswordWindow>();
                window.Owner = System.Windows.Application.Current?.MainWindow;
                var result = window.ShowDialog();
                
                if (result == true)
                {
                    _notification.ShowInfo("Password changed successfully!");
                }
                
                _logger.LogInformation("ChangePasswordAsync completed");
                await Task.CompletedTask;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "ChangePasswordAsync failed");
                _error.Handle(ex, "Failed to open change password window");
            }
        }

        private bool CanChangePassword() => !IsBusy;

        private async Task SignOutAsync()
        {
            try
            {
                _logger.LogInformation("SignOutAsync started");
                
                var currentUser = _services.GetRequiredService<ICurrentUserService>();
                currentUser.SignOut();
                
                _notification.ShowInfo("You have been signed out.");
                
                System.Windows.Application.Current.Shutdown();
                
                _logger.LogInformation("SignOutAsync completed");
                await Task.CompletedTask;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "SignOutAsync failed");
                _error.Handle(ex, "Failed to sign out");
            }
        }

        private bool CanSignOut() => !IsBusy;

        private async Task OpenSettingsAsync()
        {
            try
            {
                _logger.LogInformation("OpenSettingsAsync started");
                
                // Check if settings feature is available (optional)
                if (!_featureService.IsFeatureAvailable("Settings"))
                {
                    var reason = _featureService.GetFeatureUnavailableReason("Settings") ?? "Settings module not available";
                    _notification.ShowWarning($"Settings not available: {reason}");
                    _logger.LogWarning("Settings feature unavailable: {Reason}", reason);
                    return;
                }
                
                var app = (App)System.Windows.Application.Current;
                var window = app.Services.GetRequiredService<SettingsWindow>();
                window.Owner = System.Windows.Application.Current?.MainWindow;
                window.ShowDialog();
                
                _logger.LogInformation("OpenSettingsAsync completed");
                await Task.CompletedTask;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "OpenSettingsAsync failed");
                _error.Handle(ex, "Failed to open settings");
            }
        }

        private bool CanOpenSettings() => !IsBusy && _featureService.IsFeatureAvailable("Settings");

        private bool DetectIsAdmin()
        {
            try
            {
                if (_services == null) return false;

                object? svc = null;

                // 1) Fully qualified interface
                var qType = Type.GetType("BestFlex.Infrastructure.Auth.ICurrentUserService, BestFlex.Infrastructure");
                if (qType != null) svc = _services.GetService(qType);

                // 2) Any interface named ICurrentUserService
                if (svc == null)
                {
                    var iface = AppDomain.CurrentDomain.GetAssemblies()
                                   .SelectMany(a => SafeTypes(a))
                                   .FirstOrDefault(t => t.IsInterface && t.Name == "ICurrentUserService");
                    if (iface != null) svc = _services.GetService(iface);
                }

                // 3) Any class named *CurrentUserService
                if (svc == null)
                {
                    var impl = AppDomain.CurrentDomain.GetAssemblies()
                                  .SelectMany(a => SafeTypes(a))
                                  .FirstOrDefault(t => t.IsClass && t.Name.EndsWith("CurrentUserService", StringComparison.Ordinal));
                    if (impl != null) svc = _services.GetService(impl);
                }

                if (svc == null) return false;

                var t = svc.GetType();

                // Roles (IEnumerable<string>) or string
                var rolesProp = t.GetProperty("Roles");
                if (rolesProp != null)
                {
                    var val = rolesProp.GetValue(svc);
                    if (val is System.Collections.IEnumerable en)
                        foreach (var o in en)
                            if (string.Equals(o?.ToString(), "Admin", StringComparison.OrdinalIgnoreCase))
                                return true;

                    if (val is string s1 && CsvHasAdmin(s1)) return true;
                }

                // RolesCsv
                var csv = t.GetProperty("RolesCsv")?.GetValue(svc)?.ToString();
                if (CsvHasAdmin(csv)) return true;

                // Role (single)
                var role = t.GetProperty("Role")?.GetValue(svc)?.ToString();
                return string.Equals(role, "Admin", StringComparison.OrdinalIgnoreCase);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Admin detection failed");
                return false;
            }
        }

        private static bool CsvHasAdmin(string? csv)
        {
            if (string.IsNullOrWhiteSpace(csv)) return false;
            foreach (var r in csv.Split(new[] { ',', ';' }, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
                if (string.Equals(r, "Admin", StringComparison.OrdinalIgnoreCase)) return true;
            return false;
        }

        private static System.Collections.Generic.IEnumerable<Type> SafeTypes(System.Reflection.Assembly a)
        {
            try { return a.GetTypes(); }
            catch (System.Reflection.ReflectionTypeLoadException ex) { return ex.Types.Where(x => x != null)!; }
            catch { return System.Array.Empty<Type>(); }
        }
    }
}