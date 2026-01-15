using BestFlex.Application.Abstractions;

using BestFlex.Infrastructure.Auth;
using BestFlex.Infrastructure.Services;
using BestFlex.Persistence.Data;
using BestFlex.Persistence.Repositories;
using BestFlex.Shell.Infrastructure;
using BestFlex.Shell.Navigation;
using BestFlex.Shell.Pages;
using BestFlex.Shell.Printing;
using BestFlex.Shell.Security;
using BestFlex.Shell.UI.Toasts;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
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
        private IHost? _host;
        public IServiceProvider Services => _host!.Services;

        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);
            ShutdownMode = ShutdownMode.OnExplicitShutdown;   // show Login first

            _host = Host.CreateDefaultBuilder()
                .ConfigureServices(services =>
                {
                    // Navigator + core pages
                    services.AddSingleton<INavigator, Navigator>();
                    services.AddTransient<DashboardPage>();
                    services.AddTransient<NewSalePage>();
                    services.AddTransient<InvoicesPage>();
                    services.AddTransient<TemplateDesignerPage>();

                    // Navigation service and navigable windows
                    services.AddSingleton<BestFlex.Application.Abstractions.INavigationService, BestFlex.Shell.Services.NavigationService>();
                    services.AddTransient<InvoiceDetailsWindow>();
                    services.AddTransient<BestFlex.Shell.Windows.LowStockWindow>();
                    services.AddTransient<BestFlex.Shell.Windows.UnpaidInvoicesWindow>();

                    // Db + login stack
                    services.AddDbContext<BestFlexDbContext>(opt =>
                        opt.UseSqlite("Data Source=bestflex_local.db"));
                    services.AddSingleton<ICurrentUserService, CurrentUserService>();
                    services.AddScoped<IUserRepository, UserRepository>();
                    services.AddScoped<PasswordService>();
                    services.AddScoped<LoginService>();
                    services.AddSingleton<IAuthorizationService, AuthorizationService>();
                    



                    // Statements
                    services.AddScoped<BestFlex.Application.Abstractions.Statements.ICustomerStatementService,
                                      BestFlex.Infrastructure.Statements.CustomerStatementService>();

                    // Windows
                    services.AddTransient<BestFlex.Shell.Windows.AccountStatementWindow>();


                    // UI helpers
                    services.AddSingleton<IToastService, ToastService>();

                    // Printing
                    RegisterInvoiceEngine(services); // IInvoicePrintEngine (robust)
                    services.AddScoped<IInvoiceTemplateProvider, DbInvoiceTemplateProvider>();

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
                    TryAddTransient(services, "BestFlex.Shell.LoginWindow");
                    TryAddTransient(services, "BestFlex.Shell.SettingsWindow");
                    TryAddTransient(services, "BestFlex.Shell.ChangePasswordWindow");
                })
                .Build();

            _host.Start();

            // Apply theme on startup based on user prefs
            ThemeManager.Apply(UserPrefs.Current.Theme);

            // Ensure DB migrations
            TryMigrateDatabase(Services);

            // Ensure at least one user exists (convenience for first-run)
            EnsureDefaultUserExists(Services);

            // Routes
            var nav = Services.GetRequiredService<INavigator>();
            nav.Register("app://core/dashboard", () => Services.GetRequiredService<DashboardPage>());
            nav.Register("app://sales/new", () => Services.GetRequiredService<NewSalePage>());
            nav.Register("app://sales/invoices", () => Services.GetRequiredService<InvoicesPage>());
            nav.Register("app://core/templates", () => Services.GetRequiredService<TemplateDesignerPage>());
            TryRegisterRoute(nav, Services, "app://inventory/receive", "BestFlex.Shell.Views.Pages.Inventory.ReceiveStockPage");
            TryRegisterRoute(nav, Services, "app://sales/statements", "BestFlex.Shell.Views.Pages.Sales.CustomerStatementsPage");

            if (TryShowLogin(Services))
            {
                var main = Services.GetRequiredService<MainWindow>();
                MainWindow = main;
                main.Show();
                ShutdownMode = ShutdownMode.OnMainWindowClose;
            }
            else
            {
                Shutdown();
            }
        }

        protected override void OnExit(ExitEventArgs e)
        {
            _host?.Dispose();
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

        private static void EnsureDefaultUserExists(IServiceProvider sp)
        {
            try
            {
                using var scope = sp.CreateScope();
                var db = scope.ServiceProvider.GetRequiredService<BestFlexDbContext>();

                // If any user exists, do nothing
                if (db.Users.Any()) return;

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
            }
            catch { /* ignore failures */ }
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
    }
}
