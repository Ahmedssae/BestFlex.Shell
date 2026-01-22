using BestFlex.Application.Abstractions;
using BestFlex.Application.Mapping;
using BestFlex.Domain;
using BestFlex.Infrastructure.Services.Sales;
using BestFlex.Shell.ViewModels;
using BestFlex.Infrastructure.Commands;
using BestFlex.Infrastructure.Services;
using BestFlex.Infrastructure.Transactions;

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

namespace BestFlex.Shell
{
    public partial class App : System.Windows.Application
    {
        public IServiceProvider Services { get; private set; } = null!;

        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);

            var services = new ServiceCollection();

            // Configuration: read environment variables for kill switches and other settings
            var configuration = new ConfigurationBuilder()
                .AddEnvironmentVariables()
                .Build();
            services.AddSingleton<IConfiguration>(configuration);

            ConfigureServices(services);

            // Register the dependency health service implementation
            services.AddSingleton<BestFlex.Application.Abstractions.IDependencyHealthService, BestFlex.Infrastructure.Diagnostics.DependencyHealthService>();
            // Register module policy service (Phase 14)
            services.AddSingleton<BestFlex.Application.Abstractions.IModulePolicyService, BestFlex.Infrastructure.Services.ModulePolicyService>();
            // Production hardening: environment & kill-switch services (Phase 16)
            services.AddSingleton<BestFlex.Application.Abstractions.IEnvironmentContext, BestFlex.Infrastructure.Services.EnvironmentContext>();
            services.AddSingleton<BestFlex.Application.Abstractions.IKillSwitchService, BestFlex.Infrastructure.Services.KillSwitchService>();
            services.AddSingleton<BestFlex.Application.Abstractions.ISystemSafetyPolicy, BestFlex.Infrastructure.Services.SystemSafetyPolicy>();
            // System event sink (Phase 17) - scoped
            services.AddScoped<BestFlex.Application.Abstractions.ISystemEventSink, BestFlex.Infrastructure.Diagnostics.PersistentSystemEventSink>();
            // Data integrity validator (Phase 18)
            services.AddScoped<BestFlex.Application.Abstractions.IDataIntegrityValidator, BestFlex.Infrastructure.Diagnostics.DatabaseIntegrityValidator>();
            // Backup/restore & read-only services (Phase 19)
            services.AddSingleton<BestFlex.Application.Abstractions.IBackupService, BestFlex.Infrastructure.Diagnostics.SqliteBackupService>();
            services.AddSingleton<BestFlex.Application.Abstractions.IRestoreSimulationService, BestFlex.Infrastructure.Diagnostics.RestoreSimulationService>();
            services.AddSingleton<BestFlex.Application.Abstractions.IReadOnlyModeService, BestFlex.Infrastructure.Diagnostics.ReadOnlyModeService>();
            // Forensic logger (Phase 20)
            services.AddSingleton<BestFlex.Domain.IForensicLogger, BestFlex.Infrastructure.Diagnostics.ForensicLogger>();

            ServiceProvider = services.BuildServiceProvider();
            Services = ServiceProvider; // expose for code-behind usage

            // Forensic log: System startup
            try
            {
                var fl = Services.GetService<BestFlex.Domain.IForensicLogger>();
                fl?.LogAsync(new BestFlex.Domain.ForensicEvent(
                    BestFlex.Domain.ForensicEventType.SystemStartup,
                    DateTime.UtcNow,
                    Environment.MachineName,
                    Services.GetService<BestFlex.Application.Abstractions.ICurrentUserService>()?.Username ?? "<unknown>",
                    "Application startup",
                    null,
                    null)).GetAwaiter().GetResult();
            }
            catch { }

            // Validate critical dependency graph ONCE, immediately after provider is built and before any UI or DB operations
            var dependencyHealthService = ServiceProvider.GetRequiredService<BestFlex.Application.Abstractions.IDependencyHealthService>();
            try
            {
                dependencyHealthService.Validate(Services);
            }
            catch (InvalidOperationException ex)
            {
                // Let the process terminate by rethrowing; do not swallow or show UI here per constraints
                // Rethrow to prevent any further startup
                throw;
            }

            // Phase 18: Data integrity validation (hard stop if unhealthy)
            using (var integrityScope = ServiceProvider.CreateScope())
            {
                var integrityValidator = integrityScope.ServiceProvider.GetRequiredService<BestFlex.Application.Abstractions.IDataIntegrityValidator>();
                var integrity = integrityValidator.ValidateAsync().GetAwaiter().GetResult();
                if (!integrity.IsHealthy)
                {
                    // Enter read-only mode if supported
                    try
                    {
                        var rom = integrityScope.ServiceProvider.GetService<BestFlex.Application.Abstractions.IReadOnlyModeService>() as BestFlex.Infrastructure.Diagnostics.ReadOnlyModeService;
                        rom?.EnterReadOnlyWithLogging("Integrity / recovery failure", integrityScope.ServiceProvider);
                    }
                    catch { }
                    throw new InvalidOperationException($"CRITICAL: Data integrity validation failed. {integrity.FailureReason}");
                }
            }

            // Phase 19: Startup backup and restore simulation
            using (var backupScope = ServiceProvider.CreateScope())
            {
                var backupService = backupScope.ServiceProvider.GetRequiredService<BestFlex.Application.Abstractions.IBackupService>();
                var restoreSim = backupScope.ServiceProvider.GetRequiredService<BestFlex.Application.Abstractions.IRestoreSimulationService>();
                var readOnly = backupScope.ServiceProvider.GetRequiredService<BestFlex.Application.Abstractions.IReadOnlyModeService>();

                var backup = backupService.CreateBackupAsync().GetAwaiter().GetResult();
                if (!backup.Success)
                {
                    readOnly.EnterReadOnly("Integrity / recovery failure");
                    throw new InvalidOperationException($"CRITICAL: Startup backup failed. {backup.FailureReason}");
                }

                var canRestore = restoreSim.CanRestoreAsync(backup.BackupPath).GetAwaiter().GetResult();
                if (!canRestore)
                {
                    readOnly.EnterReadOnly("Integrity / recovery failure");
                    throw new InvalidOperationException("CRITICAL: Backup restore simulation failed.");
                }
            }

            // Ensure DB schema exists and seed user BEFORE showing UI
            using (var scope = ServiceProvider.CreateScope())
            {
                var logger = scope.ServiceProvider.GetRequiredService<Microsoft.Extensions.Logging.ILogger<App>>();
                var db = scope.ServiceProvider.GetRequiredService<BestFlexDbContext>();

                try
                {
                    var conn = db.Database.GetDbConnection().ConnectionString;
                    logger.LogInformation("Database connection: {Conn}", conn);

                    var tables = db.Model.GetEntityTypes().Select(e => e.GetTableName()).Where(n => !string.IsNullOrEmpty(n)).ToList();
                    logger.LogInformation("Mapped tables: {Tables}", string.Join(',', tables));

                    // Apply migrations (preferred). Falls through on success.
                    db.Database.Migrate();
                    logger.LogInformation("Database migrated successfully");
                }
                catch (Exception ex)
                {
                    // If migrations fail, try EnsureCreated as a fallback for dev scenarios
                    var logger2 = scope.ServiceProvider.GetRequiredService<Microsoft.Extensions.Logging.ILogger<App>>();
                    logger2.LogWarning(ex, "Database.Migrate() failed; attempting EnsureCreated()");
                    try
                    {
                        db.Database.EnsureCreated();
                        logger2.LogInformation("Database EnsureCreated succeeded");
                    }
                    catch (Exception ex2)
                    {
                        logger2.LogError(ex2, "Failed to create or migrate database");
                        throw new InvalidOperationException("Database schema not created", ex2);
                    }
                }

                // Verify Users table exists by attempting a query
                try
                {
                    var cnt = db.Users.Count();
                    logger.LogInformation("Users table row count before seeding: {Count}", cnt);

                    if (cnt == 0)
                    {
                        // Seed default admin
                        var user = new BestFlex.Domain.Entities.Users
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
                        logger.LogInformation("Users table row count after seeding: {Count}", after);
                        if (after == 0)
                            throw new InvalidOperationException("User seeding failed");
                    }
                }
                catch (Exception ex)
                {
                    var logger3 = scope.ServiceProvider.GetRequiredService<Microsoft.Extensions.Logging.ILogger<App>>();
                    logger3.LogError(ex, "Users table check or seeding failed");
                    throw new InvalidOperationException("Database schema not created", ex);
                }
            }

            var loginWindow = ServiceProvider.GetRequiredService<LoginWindow>();
            loginWindow.Show();
        }

        public IServiceProvider ServiceProvider { get; private set; } = null!;

        private void ConfigureServices(IServiceCollection services)
        {
            // Logging
            services.AddLogging();

            // Navigator + core pages
            services.AddSingleton<INavigator, Navigator>();
            services.AddTransient<DashboardPage>();
            services.AddTransient<NewSalePage>();
            services.AddTransient<InvoicesPage>();
            services.AddTransient<TemplateDesignerPage>();
            
            // ViewModels
            services.AddTransient<MainWindowViewModel>();
            services.AddTransient<LoginViewModel>();
            services.AddTransient<NewSaleViewModel>();
            services.AddTransient<DashboardViewModel>();
            services.AddTransient<InvoicesPageViewModel>();
            services.AddTransient<TemplateDesignerPageViewModel>();
            
            // Feature management
            services.AddSingleton<IFeatureService, FeatureService>();

            // Sales module gate (Phase 12: locked)
            services.AddSingleton<BestFlex.Application.Abstractions.ISalesModuleGate, BestFlex.Infrastructure.Services.SalesModuleGate>();

            // Navigation service and navigable windows
            services.AddSingleton<BestFlex.Application.Abstractions.INavigationService, BestFlex.Shell.Services.NavigationService>();
            services.AddSingleton<BestFlex.Shell.Abstractions.IShellNavigationService, BestFlex.Shell.Services.NavigationService>();
            services.AddSingleton<BestFlex.Shell.Abstractions.IShellNavigationService, BestFlex.Shell.Services.FeatureAwareNavigationService>();
            services.AddTransient<InvoiceDetailsWindow>();
            services.AddTransient<BestFlex.Shell.Windows.LowStockWindow>();
            services.AddTransient<BestFlex.Shell.Windows.UnpaidInvoicesWindow>();

            // Db + login stack
            services.AddDbContext<BestFlexDbContext>(opt =>
                opt.UseSqlite("Data Source=bestflex_local.db"));
            // Ensure DbContext can receive IReadOnlyModeService if available
            services.AddScoped<BestFlex.Application.Abstractions.IReadOnlyModeService>(sp => sp.GetService<BestFlex.Application.Abstractions.IReadOnlyModeService>() ?? null);
            // Audit service registration
            services.AddScoped<BestFlex.Application.Abstractions.IAuditService, BestFlex.Infrastructure.Services.AuditService>();
            services.AddSingleton<ICurrentUserService, CurrentUserService>();
            services.AddSingleton<IPermissionService, PermissionService>();
            services.AddSingleton<IErrorService, ErrorService>();
            services.AddSingleton<BestFlex.Application.Abstractions.IUserNotificationService, BestFlex.Shell.Services.UserNotificationService>();
            services.AddSingleton<ICacheService, BestFlex.Infrastructure.Services.CacheService>();
            services.AddScoped<IAuditService, BestFlex.Infrastructure.Services.AuditService>();
            services.AddScoped<IUserRepository, BestFlex.Persistence.Repositories.UserRepository>();
            services.AddScoped<PasswordService>();
            services.AddScoped<LoginService>();
            services.AddScoped<BestFlex.Application.Abstractions.IAuthorizationService, BestFlex.Infrastructure.Services.AuthorizationService>();
            
            // Read services for lookup data
            services.AddScoped<BestFlex.Application.Abstractions.IProductReadService, BestFlex.Infrastructure.Services.ProductReadService>();
            services.AddScoped<BestFlex.Application.Abstractions.ICustomerReadService, BestFlex.Infrastructure.Services.CustomerReadService>();
            services.AddScoped<BestFlex.Application.Abstractions.ISalesService, BestFlex.Infrastructure.Services.Sales.SalesService>();
            // Stock validation service required by SalesService
            services.AddScoped<BestFlex.Application.Abstractions.IStockValidationService, BestFlex.Infrastructure.Services.StockValidationService>();
            
            // Statements
            services.AddScoped<BestFlex.Application.Abstractions.Statements.ICustomerStatementService,
                              BestFlex.Infrastructure.Statements.CustomerStatementService>();

            // Windows
            services.AddTransient<BestFlex.Shell.Windows.AccountStatementWindow>();

            // Core services
            services.AddSingleton<IExecutionLockService, ExecutionLockService>();
            services.AddSingleton<IIdempotencyService, IdempotencyService>();
            services.AddTransient<ITransactionalCommand, TransactionalCommand>();
            services.AddScoped<IUnitOfWork, EfUnitOfWork>();
            
            // Accounting services
            services.AddSingleton<IAccountingService, AccountingService>();
            services.AddScoped<IAccountRepository, AccountRepository>();

            // UI helpers
            services.AddSingleton<IToastService, ToastService>();

            // Printing
            RegisterInvoiceEngine(services); // IInvoicePrintEngine (robust)
            services.AddScoped<IInvoiceTemplateProvider, DbInvoiceTemplateProvider>();
            services.AddSingleton<IPrintingAvailabilityService, PrintingAvailabilityService>();

            // Optional modules (reflection-safe)
            TryAdd(services, "BestFlex.Application.Abstractions.ISaleDraftHandler",
                             "BestFlex.Shell.Services.EfSaleDraftHandler", ServiceLifetime.Scoped);
            TryAdd(services, "BestFlex.Application.Abstractions.ILastInvoiceTracker",
                             "BestFlex.Shell.Services.LastInvoiceTracker", ServiceLifetime.Singleton);

            TryAdd(services, "BestFlex.Shell.Printing.IGrnPrintEngine",
                             "BestFlex.Shell.Printing.FlowDocGrnPrintEngine", ServiceLifetime.Singleton);
            TryAdd(services, "BestFlex.Application.Abstractions.Inventory.IPurchaseReceiveHandler",
                             "BestFlex.Shell.Services.NullPurchaseReceiveHandler", ServiceLifetime.Scoped);
            TryAddTransient(services, "BestFlex.Shell.Views.Pages.Inventory.ReceiveStockPage");
            TryAddTransient(services, "BestFlex.Shell.Windows.GrnPreviewWindow");

            TryAdd(services, "BestFlex.Application.Abstractions.Statements.ICustomerStatementService",
                             "BestFlex.Shell.Services.NullCustomerStatementService", ServiceLifetime.Scoped);
            TryAdd(services, "BestFlex.Shell.Printing.IStatementPrintEngine",
                             "BestFlex.Shell.Printing.FlowDocStatementPrintEngine", ServiceLifetime.Singleton);
            TryAddTransient(services, "BestFlex.Shell.Views.Pages.Sales.CustomerStatementsPage");
            TryAddTransient(services, "BestFlex.Shell.Windows.StatementPreviewWindow");

            // Windows
            services.AddTransient<MainWindow>();
            // Navigation service (centralized)
            services.AddSingleton<BestFlex.Application.Abstractions.INavigationService, BestFlex.Shell.Services.NavigationService>();
            // Ensure navigated windows are registered
            services.AddTransient<BestFlex.Shell.InvoiceDetailsWindow>();
            // NewSaleWindow removed - NewSale is a Page
            services.AddTransient<BestFlex.Shell.Windows.LowStockWindow>();
            services.AddTransient<BestFlex.Shell.Windows.UnpaidInvoicesWindow>();
            services.AddTransient<BestFlex.Shell.NewSaleWindow>();
            // ViewModels
            services.AddTransient<BestFlex.Shell.ViewModels.LowStockViewModel>();
            services.AddTransient<BestFlex.Shell.ViewModels.UnpaidInvoicesViewModel>();
            services.AddTransient<BestFlex.Shell.ViewModels.DashboardViewModel>();
            // Navigation service registration
            services.AddSingleton<BestFlex.Application.Abstractions.INavigationService, BestFlex.Shell.Services.NavigationService>();
            services.AddTransient<LoginWindow>();
            // UI exception translator
            services.AddSingleton<BestFlex.Shell.Diagnostics.UiExceptionTranslator>();
            TryAddTransient(services, "BestFlex.Shell.SettingsWindow");
            TryAddTransient(services, "BestFlex.Shell.ChangePasswordWindow");
        }

        protected override void OnExit(ExitEventArgs e)
        {
            try
            {
                var fl = Services.GetService<BestFlex.Domain.IForensicLogger>();
                fl?.LogAsync(new BestFlex.Domain.ForensicEvent(
                    BestFlex.Domain.ForensicEventType.SystemShutdown,
                    DateTime.UtcNow,
                    Environment.MachineName,
                    Services.GetService<BestFlex.Application.Abstractions.ICurrentUserService>()?.Username ?? "<unknown>",
                    "Application shutdown",
                    null,
                    null)).GetAwaiter().GetResult();
            }
            catch { }
            base.OnExit(e);
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
            return ReflectionExceptionUnwrapper.GetUserFriendlyMessage(ex);
        }

        private void SetupGlobalExceptionHandling()
        {
            // Handle UI thread exceptions
            this.DispatcherUnhandledException += (sender, e) =>
            {
                var errorService = Services?.GetService<IErrorService>();
                var notificationService = Services?.GetService<IUserNotificationService>();
                var translator = Services?.GetService<BestFlex.Shell.Diagnostics.UiExceptionTranslator>();

                // Unwrap reflection exceptions FIRST
                var unwrapped = ReflectionExceptionUnwrapper.Unwrap(e.Exception);

                // Log full exception (type + stack trace)
                errorService?.Handle(unwrapped, "Application_DispatcherUnhandledException");

                // Record system event (Critical)
                if (Services != null)
                {
                    using var scope = Services.CreateScope();
                    var sink = scope.ServiceProvider.GetService<BestFlex.Application.Abstractions.ISystemEventSink>();
                    sink?.RecordAsync(new BestFlex.Application.Abstractions.SystemEvent(
                        DateTime.UtcNow,
                        ForensicToSystemSeverityMapper.Map(BestFlex.Domain.ForensicEventType.UnexpectedException),
                        "DispatcherUnhandledException",
                        unwrapped.Message ?? string.Empty,
                        unwrapped.GetType().FullName,
                        unwrapped.StackTrace)).GetAwaiter().GetResult();

                    var fl = scope.ServiceProvider.GetService<BestFlex.Domain.IForensicLogger>();
                    fl?.LogAsync(new BestFlex.Domain.ForensicEvent(
                        BestFlex.Domain.ForensicEventType.UnexpectedException,
                        DateTime.UtcNow,
                        Environment.MachineName,
                        scope.ServiceProvider.GetService<BestFlex.Application.Abstractions.ICurrentUserService>()?.Username ?? "<unknown>",
                        unwrapped.Message ?? string.Empty,
                        null,
                        unwrapped.StackTrace)).GetAwaiter().GetResult();
                }

                // Translate only for UI display. Do not translate startup/DI failures (they occur before UI wiring)
                var message = translator?.Translate(unwrapped) ?? "An unexpected error occurred.";
                notificationService?.ShowError(message);

                // Prevent crash
                e.Handled = true;
            };

            // Handle task exceptions
            TaskScheduler.UnobservedTaskException += (sender, e) =>
            {
                var errorService = Services?.GetService<IErrorService>();
                
                // Unwrap reflection exceptions FIRST
                var unwrapped = ReflectionExceptionUnwrapper.Unwrap(e.Exception);
                errorService?.Handle(unwrapped, "TaskScheduler_UnobservedTaskException");
                
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
                            unwrapped.Message ?? string.Empty,
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
                        unwrapped.Message ?? string.Empty,
                        null,
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
                    var errorService = Services?.GetService<IErrorService>();
                    // Unwrap reflection exceptions FIRST
                    var unwrapped = ReflectionExceptionUnwrapper.Unwrap(ex);
                    errorService?.Handle(unwrapped, "AppDomain_UnhandledException");

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
                                unwrapped.Message ?? string.Empty,
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
                            unwrapped.Message ?? string.Empty,
                            null,
                            unwrapped.StackTrace)).GetAwaiter().GetResult();
                    }
                }
            };
        }
    }
}
