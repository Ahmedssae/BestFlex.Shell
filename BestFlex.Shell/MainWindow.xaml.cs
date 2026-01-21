using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using BestFlex.Application.Abstractions;
using BestFlex.Persistence.Data;
using BestFlex.Shell.Printing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace BestFlex.Shell
{
    public partial class MainWindow : Window
    {
        private readonly IPermissionService _permissions;

        public MainWindow()
        {
            InitializeComponent();
            
            var app = (App)System.Windows.Application.Current;
            _permissions = app.Services.GetRequiredService<IPermissionService>();
            DataContext = this;

            // Ctrl+Shift+P → Reprint last invoice
            var cmd = new RoutedCommand("ReprintLastInvoice", typeof(MainWindow));
            this.InputBindings.Add(new KeyBinding(cmd, new KeyGesture(Key.P, ModifierKeys.Control | ModifierKeys.Shift)));
            this.CommandBindings.Add(new CommandBinding(cmd, async (_, __) => await ReprintLastInvoiceAsync()));

            // Hide restricted nav entries once visual tree is ready
            this.Loaded += (_, __) =>
            {
                if (!DetectIsAdmin())
                    HideTemplateEntries();
            };
        }

        // Permission properties for menu binding
        public bool CanManageSettings => _permissions.CanManageSettings();

        private async Task ReprintLastInvoiceAsync()
        {
            try
            {
                var sp = ((App)System.Windows.Application.Current).Services;
                var tracker = sp.GetRequiredService<BestFlex.Shell.Services.ILastInvoiceTracker>();
                if (tracker.LastInvoiceId == null)
                {
                    MessageBox.Show("No invoice created in this session yet.", "Reprint",
                        MessageBoxButton.OK, MessageBoxImage.Information);
                    return;
                }

                var db = sp.GetRequiredService<BestFlexDbContext>();
                var tplProvider = sp.GetRequiredService<IInvoiceTemplateProvider>();
                var printEngine = sp.GetRequiredService<IInvoicePrintEngine>();

                var inv = await db.SellingInvoices
                    .Include(i => i.CustomerAccount)
                    .Include(i => i.Items).ThenInclude(it => it.Product)
                    .FirstOrDefaultAsync(i => i.Id == tracker.LastInvoiceId.Value);

                if (inv == null)
                {
                    MessageBox.Show("Last invoice not found.", "Reprint",
                        MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                var draft = new BestFlex.Shell.Models.SaleDraft
                {
                    InvoiceNumber = inv.InvoiceNo,
                    InvoiceDate = inv.IssuedAt,
                    CustomerName = inv.CustomerAccount.Name,
                    Currency = inv.Currency,
                    Subtotal = inv.Items.Sum(x => x.UnitPrice * x.Quantity),
                    DiscountPercent = 0m,
                    TaxPercent = 0m,
                    GrandTotal = inv.Items.Sum(x => x.UnitPrice * x.Quantity)
                };
                foreach (var it in inv.Items)
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

                var wnd = new BestFlex.Shell.Printing.QuickPrintPreviewWindow
                {
                    Owner = System.Windows.Application.Current?.MainWindow
                };
                wnd.SetDocument(doc);
                wnd.ShowDialog();
            }
            catch (Exception ex)
            {
                MessageBox.Show("Reprint failed:\n" + ex.Message, "Reprint",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        // ---- Role detection (reflection-only; no compile-time dependency on any auth package) ----
        private static bool CsvHasAdmin(string? csv)
        {
            if (string.IsNullOrWhiteSpace(csv)) return false;
            foreach (var r in csv.Split(new[] { ',', ';' }, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
                if (string.Equals(r, "Admin", StringComparison.OrdinalIgnoreCase)) return true;
            return false;
        }

        private bool DetectIsAdmin()
        {
            try
            {
                var sp = ((App)System.Windows.Application.Current).Services;
                if (sp == null) return false;

                object? svc = null;

                // 1) Fully qualified interface
                var qType = Type.GetType("BestFlex.Infrastructure.Auth.ICurrentUserService, BestFlex.Infrastructure");
                if (qType != null) svc = sp.GetService(qType);

                // 2) Any interface named ICurrentUserService
                if (svc == null)
                {
                    var iface = AppDomain.CurrentDomain.GetAssemblies()
                                   .SelectMany(a => SafeTypes(a))
                                   .FirstOrDefault(t => t.IsInterface && t.Name == "ICurrentUserService");
                    if (iface != null) svc = sp.GetService(iface);
                }

                // 3) Any class named *CurrentUserService
                if (svc == null)
                {
                    var impl = AppDomain.CurrentDomain.GetAssemblies()
                                  .SelectMany(a => SafeTypes(a))
                                  .FirstOrDefault(t => t.IsClass && t.Name.EndsWith("CurrentUserService", StringComparison.Ordinal));
                    if (impl != null) svc = sp.GetService(impl);
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
            catch { return false; }
        }

        private static IEnumerable<Type> SafeTypes(System.Reflection.Assembly a)
        {
            try { return a.GetTypes(); }
            catch (System.Reflection.ReflectionTypeLoadException ex) { return ex.Types.Where(x => x != null)!; }
            catch { return Array.Empty<Type>(); }
        }
        // -----------------------------------------------------------------------------------------

        // ---- Sidebar guard: hide any visual whose Tag matches Templates route or whose header/text says Templates ----
        private void HideTemplateEntries()
        {
            try
            {
                foreach (var fe in EnumerateVisuals<FrameworkElement>(this))
                {
                    if (fe == null) continue;

                    if (fe.Tag is string tag &&
                        tag.Equals("app://core/templates", StringComparison.OrdinalIgnoreCase))
                    {
                        fe.Visibility = Visibility.Collapsed;
                        continue;
                    }

                    if (fe is ContentControl cc && cc.Content is string s)
                    {
                        var txt = s.Trim();
                        if (txt.Equals("Templates", StringComparison.OrdinalIgnoreCase) ||
                            txt.Equals("Template Designer", StringComparison.OrdinalIgnoreCase))
                        {
                            fe.Visibility = Visibility.Collapsed;
                        }
                    }
                }
            }
            catch
            {
                // best-effort; never break shell
            }
        }

        private static IEnumerable<T> EnumerateVisuals<T>(DependencyObject root) where T : DependencyObject
        {
            var stack = new Stack<DependencyObject>();
            stack.Push(root);
            while (stack.Count > 0)
            {
                var d = stack.Pop();
                var n = VisualTreeHelper.GetChildrenCount(d);
                for (int i = 0; i < n; i++)
                {
                    var c = VisualTreeHelper.GetChild(d, i);
                    if (c is T t) yield return t;
                    stack.Push(c);
                }
            }
        }

        private void ChangePassword_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var app = (App)System.Windows.Application.Current;
                var window = app.Services.GetRequiredService<ChangePasswordWindow>();
                window.Owner = this;
                var result = window.ShowDialog();
                
                if (result == true)
                {
                    MessageBox.Show("Password changed successfully!", "Success",
                        MessageBoxButton.OK, MessageBoxImage.Information);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to open change password window: {ex.Message}", "Error",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void SignOut_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var app = (App)System.Windows.Application.Current;
                var currentUser = app.Services.GetRequiredService<BestFlex.Application.Abstractions.ICurrentUserService>();
                currentUser.SignOut();
                
                MessageBox.Show("You have been signed out.", "Signed Out",
                    MessageBoxButton.OK, MessageBoxImage.Information);
                
                System.Windows.Application.Current.Shutdown();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to sign out: {ex.Message}", "Error",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void OpenSettings_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var app = (App)System.Windows.Application.Current;
                var window = app.Services.GetRequiredService<SettingsWindow>();
                window.Owner = this;
                window.ShowDialog();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to open settings: {ex.Message}", "Error",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
    }
}
