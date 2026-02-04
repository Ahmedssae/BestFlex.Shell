using BestFlex.Application.Abstractions;
using BestFlex.Application.Mapping;
using BestFlex.Domain;
using BestFlex.Shell.ViewModels;
using BestFlex.Infrastructure.Commands;
using BestFlex.Infrastructure.Services;
using BestFlex.Infrastructure.Transactions;
using System.Windows.Threading;

using BestFlex.Infrastructure.Auth;
using BestFlex.Persistence.Data;
using BestFlex.Persistence.Repositories;
using BestFlex.Shell.Infrastructure;
using BestFlex.Shell.Navigation;
using BestFlex.Shell.Pages;
using BestFlex.Shell.Printing;
using BestFlex.Shell.UI.Toasts;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using System;
using System.IO;
using System.Linq;
using BestFlex.Domain.Entities;
using BCryptNet = BCrypt.Net.BCrypt;
using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Resources;

namespace BestFlex.Shell
{
    public partial class App : System.Windows.Application
    {
      static App()
{
    try
    {
        var dir = @"C:\temp";
        if (!Directory.Exists(dir))
            Directory.CreateDirectory(dir);

        File.WriteAllText(
            Path.Combine(dir, "BESTFLEX_STARTUP.txt"),
            "App static constructor executed at " + DateTime.Now + Environment.NewLine
        );
    }
    catch (Exception ex)
    {
        try
        {
            File.AppendAllText(@"C:\temp\BESTFLEX_STARTUP.txt", "Static ctor failed: " + ex + Environment.NewLine);
        }
        catch { }
    }
}

        public IServiceProvider Services { get; private set; } = null!;

        public App()
        {
            File.AppendAllText(@"C:\temp\BESTFLEX_STARTUP.txt", "App constructor executed at " + DateTime.Now + Environment.NewLine);
        }

        protected override void OnStartup(StartupEventArgs e)
        {
            // 1. FORCE SHUTDOWN MODE IMMEDIATELY
            ShutdownMode = ShutdownMode.OnMainWindowClose;

            base.OnStartup(e);

            File.AppendAllText(@"C:\temp\BESTFLEX_STARTUP.txt", "OnStartup started at " + DateTime.Now + Environment.NewLine);

            try
            {
                // 2. BUILD SERVICES FIRST
                var services = new ServiceCollection();
                ConfigureServices(services);
                ServiceProvider = services.BuildServiceProvider();
                Services = ServiceProvider;

                File.AppendAllText(@"C:\temp\BESTFLEX_STARTUP.txt", "Services built at " + DateTime.Now + Environment.NewLine);

                // Log ERP v1.0 capabilities at startup
                try
                {
                    var capabilityService = Services.GetService<Services.ICapabilityService>();
                    if (capabilityService != null)
                    {
                        capabilityService.LogCapabilities();
                        File.AppendAllText(@"C:\temp\BESTFLEX_STARTUP.txt", "Capabilities logged at " + DateTime.Now + Environment.NewLine);
                    }
                }
                catch (Exception ex)
                {
                    File.AppendAllText(@"C:\temp\BESTFLEX_STARTUP.txt", "Failed to log capabilities: " + ex.Message + Environment.NewLine);
                }

                // 3. CREATE AND SHOW LOGIN WINDOW (EXACTLY ONCE)
                var login = Services.GetRequiredService<LoginWindow>();
                MainWindow = login;
                
                File.AppendAllText(@"C:\temp\BESTFLEX_STARTUP.txt", "Login window created at " + DateTime.Now + Environment.NewLine);
                
                login.Show();
                
                File.AppendAllText(@"C:\temp\BESTFLEX_STARTUP.txt", "Login window shown at " + DateTime.Now + Environment.NewLine);

                // 4. MOVE HEAVY OPERATIONS OFF MAIN THREAD
                _ = Task.Run(async () =>
                {
                    try
                    {
                        await Task.Delay(1000); // Let UI fully initialize
                        
                        // Test Customer Management functionality
                        try
                        {
                            await Tests.CustomerManagementTest.TestCustomerManagementAsync();
                        }
                        catch (Exception testEx)
                        {
                            File.AppendAllText(@"C:\temp\BESTFLEX_STARTUP.txt", "Customer Management Test Error: " + testEx + Environment.NewLine);
                        }
                        
                        // Database operations in background
                        using var scope = Services.CreateScope();
                        var dbContext = scope.ServiceProvider.GetRequiredService<BestFlexDbContext>();
                        await dbContext.Database.EnsureCreatedAsync();
                        
                        File.AppendAllText(@"C:\temp\BESTFLEX_STARTUP.txt", "Database initialized at " + DateTime.Now + Environment.NewLine);
                    }
                    catch (Exception ex)
                    {
                        File.AppendAllText(@"C:\temp\BESTFLEX_STARTUP.txt", "Background init error: " + ex + Environment.NewLine);
                    }
                });
            }
            catch (Exception ex)
            {
                File.AppendAllText(@"C:\temp\BESTFLEX_STARTUP.txt", "FATAL STARTUP ERROR: " + ex + Environment.NewLine);
                
                // Show error and exit
                MessageBox.Show($"Application failed to start: {ex.Message}", "Startup Error", MessageBoxButton.OK, MessageBoxImage.Error);
                Shutdown(1);
            }
        }

        public IServiceProvider ServiceProvider { get; private set; } = null!;

        private void ConfigureServices(IServiceCollection services)
        {
            // Logging
            services.AddLogging();

            // Core UI services only
            services.AddSingleton<INavigator, Navigator>();
            services.AddTransient<LoginWindow>();
            services.AddTransient<MainWindow>();
            
            // ViewModels
            services.AddTransient<LoginViewModel>();
            services.AddTransient<MainWindowViewModel>();

            // Add missing forensic logger
            services.AddSingleton<BestFlex.Domain.IForensicLogger, BestFlex.Infrastructure.Diagnostics.ForensicLogger>();

            // Use Cases (required by UI adapters)
            services.AddSingleton<BestFlex.Application.UseCases.ICreateSalesOrderUseCase, BestFlex.Application.UseCases.CreateSalesOrderUseCase>();
            services.AddSingleton<BestFlex.Application.UseCases.ICancelSalesOrderUseCase, BestFlex.Application.UseCases.CancelSalesOrderUseCase>();
            services.AddSingleton<BestFlex.Application.UseCases.IReserveStockForOrderUseCase, BestFlex.Application.UseCases.ReserveStockForOrderUseCase>();
            services.AddSingleton<BestFlex.Application.UseCases.ICheckCreditLimitUseCase, BestFlex.Application.UseCases.CheckCreditLimitUseCase>();
            services.AddSingleton<BestFlex.Application.UseCases.IReceiveStockUseCase, BestFlex.Application.UseCases.ReceiveStockUseCase>();
            services.AddSingleton<BestFlex.Application.UseCases.IAdjustStockUseCase, BestFlex.Application.UseCases.AdjustStockUseCase>();
            services.AddSingleton<BestFlex.Application.UseCases.IReserveStockUseCase, BestFlex.Application.UseCases.ReserveStockUseCase>();
            services.AddSingleton<BestFlex.Application.UseCases.ICreateProductUseCase, BestFlex.Application.UseCases.CreateProductUseCase>();
            services.AddSingleton<BestFlex.Application.UseCases.IUpdateProductUseCase, BestFlex.Application.UseCases.UpdateProductUseCase>();
            services.AddSingleton<BestFlex.Application.UseCases.IAddPriceTierUseCase, BestFlex.Application.UseCases.AddPriceTierUseCase>();
            services.AddSingleton<BestFlex.Application.UseCases.IDeactivateProductUseCase, BestFlex.Application.UseCases.DeactivateProductUseCase>();
            services.AddSingleton<BestFlex.Application.UseCases.ICreateCustomerUseCase, BestFlex.Application.UseCases.CreateCustomerUseCase>();
            services.AddSingleton<BestFlex.Application.UseCases.IUpdateCustomerUseCase, BestFlex.Application.UseCases.UpdateCustomerUseCase>();
            services.AddSingleton<BestFlex.Application.UseCases.IChangeCreditLimitUseCase, BestFlex.Application.UseCases.ChangeCreditLimitUseCase>();
            services.AddSingleton<BestFlex.Application.UseCases.IDeactivateCustomerUseCase, BestFlex.Application.UseCases.DeactivateCustomerUseCase>();

            // UI Adapters (Rebuilt Timeline - Phase 7A)
            services.AddSingleton<BestFlex.Application.UI.ICustomerUiAdapter, BestFlex.Application.UI.CustomerUiAdapter>();
            services.AddSingleton<BestFlex.Application.UI.IProductUiAdapter, BestFlex.Application.UI.ProductUiAdapter>();
            services.AddSingleton<BestFlex.Application.UI.ISalesOrderUiAdapter, BestFlex.Application.UI.SalesOrderUiAdapter>();
            services.AddSingleton<BestFlex.Application.UI.IInventoryUiAdapter, BestFlex.Application.UI.InventoryUiAdapter>();
            services.AddSingleton<BestFlex.Application.UI.IInvoicePdfExporter, BestFlex.Application.UI.InvoicePdfExporter>();
            services.AddSingleton<BestFlex.Application.UI.IPaymentUiAdapter, BestFlex.Application.UI.PaymentUiAdapter>();

            // ViewModels (Minimal for Phase 7A)
            services.AddTransient<BestFlex.Shell.ViewModels.LoginViewModel>();
            services.AddTransient<BestFlex.Shell.ViewModels.MainWindowViewModel>();
            services.AddTransient<BestFlex.Shell.ViewModels.DashboardViewModel>();
            services.AddTransient<BestFlex.Shell.ViewModels.NewSaleViewModel>();
            services.AddTransient<BestFlex.Shell.ViewModels.InvoiceListViewModel>();
            services.AddTransient<BestFlex.Shell.ViewModels.InvoiceDetailsViewModel>();
            services.AddTransient<BestFlex.Shell.ViewModels.UnpaidInvoicesViewModel>();
            services.AddTransient<BestFlex.Shell.ViewModels.LowStockViewModel>();
            services.AddTransient<BestFlex.Shell.ViewModels.ChangePasswordViewModel>();
            services.AddTransient<BestFlex.Shell.ViewModels.SafeFallbackViewModel>();

            // Customer ViewModels (Phase 7B)
            services.AddTransient<BestFlex.Shell.ViewModels.CustomerListViewModel>();
            services.AddTransient<BestFlex.Shell.ViewModels.CustomerEditViewModel>();

            // Product ViewModels (Phase 7C)
            services.AddTransient<BestFlex.Shell.ViewModels.ProductListViewModel>();
            services.AddTransient<BestFlex.Shell.ViewModels.ProductEditViewModel>();

            // Inventory ViewModels (Phase 8A)
            services.AddTransient<BestFlex.Shell.ViewModels.InventoryOverviewViewModel>();

            // Inventory Operations ViewModels (Phase 8B)
            services.AddTransient<BestFlex.Shell.ViewModels.ReceiveStockViewModel>();
            services.AddTransient<BestFlex.Shell.ViewModels.AdjustStockViewModel>();

            // Sales Order ViewModels (Phase 9A)
            services.AddTransient<BestFlex.Shell.ViewModels.SalesOrderViewModel>();

            // Invoice & Payment ViewModels (Phase 9B)
            services.AddTransient<BestFlex.Shell.ViewModels.InvoicePostingViewModel>();
            services.AddTransient<BestFlex.Shell.ViewModels.PaymentRegistrationViewModel>();

            // Real Dashboard ViewModel (Phase 10)
            services.AddTransient<BestFlex.Shell.ViewModels.RealDashboardViewModel>();

            // Database (minimal)
            var dbPath = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "BestFlex",
                "bestflex.db");
            
            Directory.CreateDirectory(Path.GetDirectoryName(dbPath)!);
            
            services.AddDbContext<BestFlexDbContext>(opt =>
                opt.UseSqlite($"Data Source={dbPath}"));

            // Basic services
            services.AddSingleton<ICurrentUserService, CurrentUserService>();
            services.AddSingleton<IPermissionService, PermissionService>();
            services.AddSingleton<IErrorService, ErrorService>();
            services.AddSingleton<BestFlex.Application.Abstractions.IUserNotificationService, BestFlex.Shell.Services.UserNotificationService>();
            
            // Navigation
            // SINGLE CONTENT HOST CONTRACT
            services.AddSingleton<BestFlex.Shell.Abstractions.IMainContentHost>(provider => 
            {
                // Register MainWindow as IMainContentHost
                return provider.GetRequiredService<MainWindow>();
            });
            
            // EXPLICIT VIEW FACTORY
            services.AddSingleton<BestFlex.Shell.Factories.ViewFactory>();
            
            // Module Gates
            services.AddSingleton<BestFlex.Application.Abstractions.ISalesModuleGate, BestFlex.Shell.Services.SalesModuleGate>();

            // Windows
            services.AddSingleton<MainWindow>(); // Register as singleton for IMainContentHost
            services.AddTransient<DashboardPage>();
            services.AddTransient<NewSalePage>();
            services.AddTransient<InvoicesPage>();
            services.AddTransient<TemplateDesignerPage>();
            
            // ViewModels
            services.AddTransient<DashboardViewModel>();
            services.AddTransient<InvoicesPageViewModel>();
            services.AddTransient<TemplateDesignerPageViewModel>();
            
            // Feature management
            services.AddSingleton<IFeatureService, FeatureService>();

            // Sales service (minimal)
            services.AddScoped<BestFlex.Application.Abstractions.ISalesService, BestFlex.Application.Services.SalesService>();

            // UI helpers
            services.AddSingleton<IToastService, ToastService>();

            // Read services
            services.AddScoped<BestFlex.Application.Abstractions.IProductReadService, BestFlex.Infrastructure.Services.ProductReadService>();
            services.AddScoped<BestFlex.Application.Abstractions.ICustomerReadService, BestFlex.Infrastructure.Services.CustomerReadService>();
            
            // Core services
            services.AddSingleton<IExecutionLockService, ExecutionLockService>();
            services.AddSingleton<IIdempotencyService, IdempotencyService>();
            services.AddTransient<ITransactionalCommand, TransactionalCommand>();
            services.AddScoped<IUnitOfWork, EfUnitOfWork>();
            
            // Accounting
            services.AddSingleton<IAccountingService, AccountingService>();
            services.AddScoped<IAccountRepository, AccountRepository>();

            // Printing
            RegisterInvoiceEngine(services);
            services.AddScoped<IInvoiceTemplateProvider, DbInvoiceTemplateProvider>();
            services.AddSingleton<IPrintingAvailabilityService, PrintingAvailabilityService>();

            // Additional windows
            services.AddTransient<InvoiceDetailsWindow>();
            services.AddTransient<BestFlex.Shell.Windows.LowStockWindow>();
            services.AddTransient<BestFlex.Shell.Windows.UnpaidInvoicesWindow>();
            services.AddTransient<BestFlex.Shell.NewSaleWindow>();
            
            // Additional ViewModels
            services.AddTransient<BestFlex.Shell.ViewModels.LowStockViewModel>();
            services.AddTransient<BestFlex.Shell.ViewModels.UnpaidInvoicesViewModel>();

            // Auth services
            services.AddScoped<PasswordService>();
            services.AddScoped<LoginService>();
            services.AddScoped<BestFlex.Application.Abstractions.IAuthorizationService, BestFlex.Infrastructure.Services.AuthorizationService>();
            services.AddScoped<IUserRepository, BestFlex.Persistence.Repositories.UserRepository>();
            services.AddScoped<IAuditService, BestFlex.Infrastructure.Services.AuditService>();
            services.AddScoped<BestFlex.Application.Abstractions.IStockValidationService, BestFlex.Application.Services.StockValidationService>();
            
            // Statements
            services.AddScoped<BestFlex.Application.Abstractions.Statements.ICustomerStatementService,
                              BestFlex.Infrastructure.Statements.CustomerStatementService>();

            // Windows for statements
            services.AddTransient<BestFlex.Shell.Windows.AccountStatementWindow>();

            // UI exception translator
            services.AddSingleton<BestFlex.Shell.Diagnostics.UiExceptionTranslator>();

            // Optional modules (reflection-safe)
            TryAdd(services, "BestFlex.Application.Abstractions.ISaleDraftHandler",
                             "BestFlex.Shell.Services.EfSaleDraftHandler", ServiceLifetime.Scoped);
            TryAdd(services, "BestFlex.Application.Abstractions.ILastInvoiceTracker",
                             "BestFlex.Shell.Services.LastInvoiceTracker", ServiceLifetime.Singleton);

            TryAdd(services, "BestFlex.Shell.Printing.IStatementPrintEngine",
                             "BestFlex.Shell.Printing.FlowDocStatementPrintEngine", ServiceLifetime.Singleton);
            TryAddTransient(services, "BestFlex.Shell.Views.Pages.Sales.CustomerStatementsPage");
            TryAddTransient(services, "BestFlex.Shell.Windows.StatementPreviewWindow");

            TryAddTransient(services, "BestFlex.Shell.SettingsWindow");
            TryAddTransient(services, "BestFlex.Shell.ChangePasswordWindow");
            
            // Add database resilience service
            services.AddSingleton<BestFlex.Infrastructure.Services.IDatabaseResilienceService, BestFlex.Infrastructure.Services.DatabaseResilienceService>();
            
            // Add startup safety service
            services.AddSingleton<BestFlex.Infrastructure.Services.IStartupSafetyService, BestFlex.Infrastructure.Services.StartupSafetyService>();
            
            // Add busy service for global loading states
            services.AddSingleton<BestFlex.Shell.Services.IBusyService, BestFlex.Shell.Services.BusyService>();
            
            // Add domain exception user message mapper
            services.AddSingleton<BestFlex.Shell.Services.IDomainExceptionUserMessageMapper, BestFlex.Shell.Services.DomainExceptionUserMessageMapper>();
            
            // Add confirmation dialog service
            services.AddSingleton<BestFlex.Shell.Services.IConfirmationDialogService, BestFlex.Shell.Services.ConfirmationDialogService>();
            
            // Add session reliability service
            services.AddSingleton<BestFlex.Shell.Services.ISessionReliabilityService, BestFlex.Shell.Services.SessionReliabilityService>();
            
            // Add audit confidence service
            services.AddSingleton<BestFlex.Shell.Services.IAuditConfidenceService, BestFlex.Shell.Services.AuditConfidenceService>();
            
            // Add fail-safe mode service
            services.AddSingleton<BestFlex.Shell.Services.IFailSafeModeService, BestFlex.Shell.Services.FailSafeModeService>();
            
            // Add business-safe defaults service
            services.AddSingleton<BestFlex.Shell.Services.IBusinessSafeDefaultsService, BestFlex.Shell.Services.BusinessSafeDefaultsService>();
            
            // Add irreversible action protection service
            services.AddSingleton<BestFlex.Shell.Services.IIrreversibleActionProtectionService, BestFlex.Shell.Services.IrreversibleActionProtectionService>();
            
            // Add read-only mode enforcement service
            services.AddSingleton<BestFlex.Shell.Services.IReadOnlyModeEnforcementService, BestFlex.Shell.Services.ReadOnlyModeEnforcementService>();
            
            // Add data consistency assertion service
            services.AddSingleton<BestFlex.Shell.Services.IDataConsistencyAssertionService, BestFlex.Shell.Services.DataConsistencyAssertionService>();
            
            // Add correlation and traceability services
            services.AddSingleton<BestFlex.Shell.Services.ICorrelationService, BestFlex.Shell.Services.CorrelationService>();
            services.AddSingleton<BestFlex.Shell.Services.IStructuredLoggingService, BestFlex.Shell.Services.StructuredLoggingService>();
            
            // Add crash recovery services
            services.AddSingleton<BestFlex.Shell.Services.ICrashRecoveryService, BestFlex.Shell.Services.CrashRecoveryService>();
            services.AddSingleton<BestFlex.Shell.Services.GlobalExceptionHandler>();
            
            // Add backup and rollback services
            services.AddSingleton<BestFlex.Shell.Services.IBackupRollbackService, BestFlex.Shell.Services.BackupRollbackService>();
            
            // Add admin visibility services
            services.AddSingleton<BestFlex.Shell.Services.IAdminVisibilityService, BestFlex.Shell.Services.AdminVisibilityService>();
            
            // Add capability service for ERP v1 scope enforcement
            services.AddSingleton<BestFlex.Shell.Services.ICapabilityService, BestFlex.Shell.Services.CapabilityService>();
            
            // Add versioning and build identity services
            services.AddSingleton<BestFlex.Shell.Services.IVersioningService, BestFlex.Shell.Services.VersioningService>();
            
            // Add environment separation services
            services.AddSingleton<BestFlex.Shell.Services.IEnvironmentService, BestFlex.Shell.Services.EnvironmentService>();
            
            // Add database migration safety services
            services.AddSingleton<BestFlex.Shell.Services.IDatabaseMigrationService, BestFlex.Shell.Services.DatabaseMigrationService>();
            
            // Add installation and first-run services
            services.AddSingleton<BestFlex.Shell.Services.IInstallationService, BestFlex.Shell.Services.InstallationService>();
            
            // Add configuration discipline services
            services.AddSingleton<BestFlex.Shell.Services.IConfigurationService, BestFlex.Shell.Services.ConfigurationService>();
            
            // Add session store for New Sale draft persistence
            services.AddSingleton<INewSaleDraftSession, NewSaleDraftSession>();
            
            // Add release safety rules services
            services.AddSingleton<BestFlex.Shell.Services.IReleaseSafetyService, BestFlex.Shell.Services.ReleaseSafetyService>();
        }

        protected override void OnExit(ExitEventArgs e)
        {
            try
            {
                File.AppendAllText(@"C:\temp\BESTFLEX_STARTUP.txt", "OnExit called at " + DateTime.Now + Environment.NewLine);
            }
            catch { }
            base.OnExit(e);
        }

        private static void RegisterSafeNavigation()
        {
            try
            {
                var app = (App)System.Windows.Application.Current;
                if (app?.Services == null) return;
                
                // Initialize RouteRegistry with service provider for DI support
                BestFlex.Shell.Navigation.RouteRegistry.Initialize(app.Services);
                
                var navigator = app.Services.GetService<BestFlex.Shell.Navigation.INavigator>();
                if (navigator == null) return;
                
                // Register dashboard page with safe factory - ALWAYS AVAILABLE
                navigator.Register("dashboard", () =>
                {
                    try
                    {
                        // Dashboard is NOT a module and must ALWAYS be available
                        // Create DashboardViewModel without any required gated services
                        var vm = new BestFlex.Shell.ViewModels.DashboardViewModel();
                        return new BestFlex.Shell.Pages.DashboardPage(vm);
                    }
                    catch (Exception ex)
                    {
                        // Even if dashboard creation fails, provide a fallback
                        var logger = app.Services.GetService<Microsoft.Extensions.Logging.ILogger<App>>();
                        logger?.LogError(ex, "Failed to create dashboard page, using fallback");
                        return new BestFlex.Shell.Pages.SafeFallbackPage("Dashboard temporarily unavailable");
                    }
                });
            }
            catch
            {
                // Registration failures should not crash startup
            }
        }

        private static void ValidateThemeContract()
        {
            try
            {
                // Reflect over ThemeKeys to get all required keys
                var themeKeysType = typeof(BestFlex.Shell.Theme.ThemeKeys);
                var fields = themeKeysType.GetFields(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static);
                
                var requiredKeys = fields
                    .Where(f => f.FieldType == typeof(string) && f.IsLiteral)
                    .Select(f => f.GetValue(null)?.ToString())
                    .Where(key => !string.IsNullOrEmpty(key))
                    .ToList();

                // Load merged theme resources (System.Windows.Application.Current.Resources)
                var appResources = System.Windows.Application.Current.Resources;
                if (appResources == null)
                {
                    throw new InvalidOperationException("CRITICAL: Application.Resources is null");
                }

                var missingKeys = new List<string>();
                foreach (var key in requiredKeys)
                {
                    if (key != null && !appResources.Contains(key))
                        missingKeys.Add(key);
                }

                if (missingKeys.Any())
                {
                    throw new InvalidOperationException(
                        $"CRITICAL: Theme contract validation failed. Missing keys: {string.Join(", ", missingKeys)}");
                }
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException($"CRITICAL: Theme contract validation failed. {ex.Message}", ex);
            }
        }

        private static void TryMigrateDatabase(IServiceProvider sp)
        {
            try
            {
                using var scope = sp.CreateScope();
                var db = scope.ServiceProvider.GetRequiredService<BestFlexDbContext>();
                db.Database.Migrate();
            }
            catch { }
        }

        private static void ValidateCoreDependencies(IServiceProvider services)
        {
            try
            {
                // Validate NewSaleViewModel can resolve all required dependencies
                var newSaleVm = services.GetRequiredService<NewSaleViewModel>();
                _ = newSaleVm; // Suppress unused variable warning
            }
            catch (InvalidOperationException ex)
            {
                var message = ReflectionExceptionUnwrapper.GetUserFriendlyMessage(ex);
                MessageBox.Show(
                    $"CRITICAL: Core dependency validation failed.\n\n{message}\n\nThe application cannot continue.",
                    "BestFlex ERP - Dependency Validation Failed",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
                
                // Log the failure
                var logger = services.GetService<ILogger<App>>();
                logger?.LogError(ex, "Core dependency validation failed");
                
                // Shutdown the application
                System.Windows.Application.Current.Shutdown(1);
            }
            catch (Exception ex)
            {
                var message = ReflectionExceptionUnwrapper.GetUserFriendlyMessage(ex);
                MessageBox.Show(
                    $"CRITICAL: Unexpected error during dependency validation.\n\n{message}\n\nThe application cannot continue.",
                    "BestFlex ERP - Validation Error",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
                
                // Log the failure
                var logger = services.GetService<ILogger<App>>();
                logger?.LogError(ex, "Unexpected error during dependency validation");
                
                // Shutdown the application
                System.Windows.Application.Current.Shutdown(1);
            }
        }

        private static void EnsureDefaultUserExists(IServiceProvider sp)
        {
            using var scope = sp.CreateScope();
            var factory = scope.ServiceProvider.GetRequiredService<IServiceScopeFactory>();
            using var inner = factory.CreateScope();
            var logger = inner.ServiceProvider.GetService<Microsoft.Extensions.Logging.ILogger<App>>();
            var db = inner.ServiceProvider.GetRequiredService<BestFlexDbContext>();

            var conn = db.Database.GetDbConnection().ConnectionString;
            logger?.LogInformation("EnsureDefaultUserExists using DB: {Conn}", conn);

            // If any user exists, do nothing
            var count = db.Users.Count();
            if (count > 0)
            {
                logger?.LogInformation("EnsureDefaultUserExists - users already present: {Count}", count);
                return;
            }

            // Create a default admin user (username: admin, password: admin)
            var user = new Users
            {
                Id = Guid.NewGuid(),
                Username = "admin",
                DisplayName = "Administrator",
                PasswordHash = BCryptNet.HashPassword("admin"),
                RolesCsv = "Admin",
                CreatedAtUtc = DateTime.UtcNow
            };
            db.Users.Add(user);
            db.SaveChanges();

            var after = db.Users.Count();
            logger?.LogInformation("EnsureDefaultUserExists - users after seeding: {Count}", after);

            if (after == 0)
                throw new InvalidOperationException("User seeding failed");
        }

        private static void RegisterInvoiceEngine(IServiceCollection services)
        {
            var iface = typeof(IInvoicePrintEngine);
            var impl = Type.GetType("BestFlex.Printing.FlowDocInvoicePrintEngine, BestFlex.Printing", false);
            if (impl == null)
            {
                var asm = TryLoadAssemblyNearby("BestFlex.Printing.dll");
                if (asm != null) impl = asm.GetType("BestFlex.Printing.FlowDocInvoicePrintEngine", false);
            }
            if (impl != null && iface.IsAssignableFrom(impl))
            {
                services.AddSingleton(iface, impl);
                return;
            }
            services.AddSingleton(iface, sp =>
            {
                var proxy = MissingEngineProxy.Create<IInvoicePrintEngine>(() =>
                    MessageBox.Show("Invoice printing module is not installed. Include BestFlex.Printing to enable printing/export.",
                                    "BestFlex Printing", MessageBoxButton.OK, MessageBoxImage.Information));
                return proxy!;
            });
        }

        private static Assembly? TryLoadAssemblyNearby(string fileName)
        {
            try
            {
                var baseDir = AppDomain.CurrentDomain.BaseDirectory;
                var match = Directory.EnumerateFiles(baseDir, fileName, SearchOption.AllDirectories).FirstOrDefault();
                if (match != null) return Assembly.LoadFrom(match);
            }
            catch { }
            return null;
        }

        private class MissingEngineProxy : DispatchProxy
        {
            private Action? _notify;
            public static T? Create<T>(Action notify) where T : class
            {
                var p = Create<T, MissingEngineProxy>() as MissingEngineProxy;
                p!._notify = notify;
                return p as T;
            }
            protected override object? Invoke(MethodInfo? targetMethod, object?[]? args)
            {
                _notify?.Invoke();
                if (targetMethod == null) return null;
                var ret = targetMethod.ReturnType;
                if (ret == typeof(void)) return null;
                return ret.IsValueType ? Activator.CreateInstance(ret) : null;
            }
        }

        private static bool TryShowLogin(IServiceProvider sp)
        {
            var t = FindType("BestFlex.Shell.LoginWindow");
            if (t == null) return true;

            var obj = sp.GetService(t) as Window ?? Activator.CreateInstance(t) as Window;
            if (obj == null) return true;

            obj.WindowStartupLocation = WindowStartupLocation.CenterScreen;
            return obj.ShowDialog() == true;
        }

        private static void TryAdd(IServiceCollection services, string serviceFullName, string implFullName, ServiceLifetime lifetime)
        {
            var serviceType = FindType(serviceFullName);
            var implType = FindType(implFullName);
            if (serviceType == null || implType == null) return;
            if (!serviceType.IsAssignableFrom(implType)) return;
            services.Add(new ServiceDescriptor(serviceType, implType, lifetime));
        }

        private static void TryAddTransient(IServiceCollection services, string fullName)
        {
            var t = FindType(fullName);
            if (t == null) return;
            services.AddTransient(t);
        }

        private static void TryRegisterRoute(INavigator nav, IServiceProvider sp, string route, string pageFullName)
        {
            var t = FindType(pageFullName);
            if (t == null) return;

            nav.Register(route, () =>
            {
                var obj = sp.GetService(t);
                if (obj is UserControl uc) return uc;
                if (obj is FrameworkElement fe) return new UserControl { Content = fe };

                return new UserControl
                {
                    Content = new Border
                    {
                        Padding = new Thickness(16),
                        Child = new TextBlock
                        {
                            Text = $"Failed to load: {pageFullName}",
                            TextWrapping = TextWrapping.Wrap
                        }
                    }
                };
            });
        }

        private static Type? FindType(string fullName)
        {
            foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
            {
                var t = asm.GetType(fullName, throwOnError: false, ignoreCase: false);
                if (t != null) return t;
            }
            return Type.GetType(fullName, throwOnError: false);
        }

        private string GetUserFriendlyMessage(Exception ex)
        {
            // Use the canonical reflection unwrapper
            return BestFlex.Application.Abstractions.ReflectionExceptionUnwrapper.GetUserFriendlyMessage(ex);
        }

        private string GenerateCorrelationId()
        {
            var guidStr = Guid.NewGuid().ToString("N");
            return $"ERR-{DateTime.UtcNow:yyyyMMdd-HHmmss}-{guidStr.Substring(0, 8).ToUpper()}";
        }

        private void LogExceptionWithCorrelation(Exception ex, string correlationId, string context, IErrorService? errorService)
        {
            try
            {
                var logger = Services?.GetService<Microsoft.Extensions.Logging.ILogger<App>>();
                
                // Log full exception details with correlation ID
                logger?.LogError(ex, 
                    "[{Context}] [CID:{CorrelationId}] {ExceptionType}: {Message}\n{StackTrace}",
                    context,
                    correlationId,
                    ex.GetType().FullName,
                    ex.Message,
                    ex.StackTrace);
                
                // Also use the error service for additional logging
                errorService?.Handle(ex, $"{context} [CID:{correlationId}]");
            }
            catch
            {
                // Last resort - don't let logging failures crash the app
                System.Diagnostics.Debug.WriteLine($"Failed to log exception: {ex.Message}");
            }
        }

        private void SetupGlobalExceptionHandling()
        {
            // Handle UI thread exceptions
            this.DispatcherUnhandledException += (sender, e) =>
            {
                var correlationId = GenerateCorrelationId();
                var errorService = Services?.GetService<IErrorService>();
                var notificationService = Services?.GetService<IUserNotificationService>();
                var translator = Services?.GetService<BestFlex.Shell.Diagnostics.UiExceptionTranslator>();

                // Unwrap reflection exceptions FIRST
                var unwrapped = BestFlex.Application.Abstractions.ReflectionExceptionUnwrapper.Unwrap(e.Exception);
                
                // Log full exception details with correlation ID
                LogExceptionWithCorrelation(unwrapped, correlationId, "Application_DispatcherUnhandledException", errorService);

                // Record system event (Critical)
                if (Services != null)
                {
                    using var scope = Services.CreateScope();
                    var sink = scope.ServiceProvider.GetService<BestFlex.Application.Abstractions.ISystemEventSink>();
                    sink?.RecordAsync(new BestFlex.Application.Abstractions.SystemEvent(
                        DateTime.UtcNow,
                        ForensicToSystemSeverityMapper.Map(BestFlex.Domain.ForensicEventType.UnexpectedException),
                        "DispatcherUnhandledException",
                        $"[CID:{correlationId}] {unwrapped.Message ?? string.Empty}",
                        unwrapped.GetType().FullName,
                        unwrapped.StackTrace)).GetAwaiter().GetResult();

                    var fl = scope.ServiceProvider.GetService<BestFlex.Domain.IForensicLogger>();
                    fl?.LogAsync(new BestFlex.Domain.ForensicEvent(
                        BestFlex.Domain.ForensicEventType.UnexpectedException,
                        DateTime.UtcNow,
                        Environment.MachineName,
                        scope.ServiceProvider.GetService<BestFlex.Application.Abstractions.ICurrentUserService>()?.Username ?? "<unknown>",
                        $"[CID:{correlationId}] {unwrapped.Message ?? string.Empty}",
                        correlationId,
                        unwrapped.StackTrace)).GetAwaiter().GetResult();
                }

                // Show user-safe error dialog with correlation ID
                var message = translator?.Translate(unwrapped) ?? "An unexpected error occurred.";
                var userMessage = $"{message}\n\nError ID: {correlationId}\nPlease report this to support.";
                notificationService?.ShowError(userMessage);

                // Prevent crash
                e.Handled = true;
            };

            // Handle task exceptions
            TaskScheduler.UnobservedTaskException += (sender, e) =>
            {
                var correlationId = GenerateCorrelationId();
                var errorService = Services?.GetService<IErrorService>();
                
                // Unwrap reflection exceptions FIRST
                var unwrapped = BestFlex.Application.Abstractions.ReflectionExceptionUnwrapper.Unwrap(e.Exception);
                
                // Log full exception details with correlation ID
                LogExceptionWithCorrelation(unwrapped, correlationId, "TaskScheduler_UnobservedTaskException", errorService);
                
                // Record system event (Error)
                if (Services != null)
                {
                    using var scope = Services.CreateScope();
                    var sink = scope.ServiceProvider.GetService<BestFlex.Application.Abstractions.ISystemEventSink>();
                    if (sink != null)
                    {
                        var se = new BestFlex.Application.Abstractions.SystemEvent(
                            DateTime.UtcNow,
                            BestFlex.Application.Abstractions.SystemEventSeverity.Error,
                            "TaskScheduler_UnobservedTaskException",
                            $"[CID:{correlationId}] {unwrapped.Message ?? string.Empty}",
                            unwrapped.GetType().FullName,
                            unwrapped.StackTrace);
                        sink.RecordAsync(se).GetAwaiter().GetResult();
                    }
                }
                if (Services != null)
                {
                    using var scope = Services.CreateScope();
                    var fl = scope.ServiceProvider.GetService<BestFlex.Domain.IForensicLogger>();
                    fl?.LogAsync(new BestFlex.Domain.ForensicEvent(
                        BestFlex.Domain.ForensicEventType.UnexpectedException,
                        DateTime.UtcNow,
                        Environment.MachineName,
                        scope.ServiceProvider.GetService<BestFlex.Application.Abstractions.ICurrentUserService>()?.Username ?? "<unknown>",
                        $"[CID:{correlationId}] {unwrapped.Message ?? string.Empty}",
                        correlationId,
                        unwrapped.StackTrace)).GetAwaiter().GetResult();
                }
                
                // Mark as observed to prevent process termination
                e.SetObserved();
            };

            // Handle domain exceptions
            AppDomain.CurrentDomain.UnhandledException += (sender, e) =>
            {
                if (e.ExceptionObject is Exception ex)
                {
                    var correlationId = GenerateCorrelationId();
                    var errorService = Services?.GetService<IErrorService>();
                    // Unwrap reflection exceptions FIRST
                    var unwrapped = BestFlex.Application.Abstractions.ReflectionExceptionUnwrapper.Unwrap(ex);
                    errorService?.Handle(unwrapped, $"AppDomain_UnhandledException [CID:{correlationId}]");

                    // Record system event (Critical)
                    if (Services != null)
                    {
                        using var scope = Services.CreateScope();
                        var sink = scope.ServiceProvider.GetService<BestFlex.Application.Abstractions.ISystemEventSink>();
                        if (sink != null)
                        {
                            var se = new BestFlex.Application.Abstractions.SystemEvent(
                                DateTime.UtcNow,
                                ForensicToSystemSeverityMapper.Map(BestFlex.Domain.ForensicEventType.Critical),
                                "AppDomain_UnhandledException",
                                $"[CID:{correlationId}] {unwrapped.Message ?? string.Empty}",
                                unwrapped.GetType().FullName,
                                unwrapped.StackTrace);
                            sink.RecordAsync(se).GetAwaiter().GetResult();
                        }
                    }
                    if (Services != null)
                    {
                        using var scope = Services.CreateScope();
                        var fl = scope.ServiceProvider.GetService<BestFlex.Domain.IForensicLogger>();
                        fl?.LogAsync(new BestFlex.Domain.ForensicEvent(
                            BestFlex.Domain.ForensicEventType.Critical,
                            DateTime.UtcNow,
                            Environment.MachineName,
                            scope.ServiceProvider.GetService<BestFlex.Application.Abstractions.ICurrentUserService>()?.Username ?? "<unknown>",
                            $"[CID:{correlationId}] {unwrapped.Message ?? string.Empty}",
                            correlationId,
                            unwrapped.StackTrace)).GetAwaiter().GetResult();
                    }

                    // Show critical error dialog with correlation ID
                    System.Windows.Application.Current.Dispatcher.Invoke(() =>
                    {
                        var message = $"A critical error has occurred and the application may become unstable.\n\nError ID: {correlationId}\nPlease save your work and restart the application.";
                        MessageBox.Show(message, "Critical Error", MessageBoxButton.OK, MessageBoxImage.Error);
                    });
                }
            };
        }
    }
}
