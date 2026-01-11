using BestFlex.Shell.Infrastructure;
using BestFlex.Shell.Navigation;
using System;
using System.Linq;
using System.Windows;
using System.Windows.Controls;

namespace BestFlex.Shell
{
    public partial class MainWindow : Window
    {
        private bool _wired;

        protected override void OnContentRendered(EventArgs e)
        {
            base.OnContentRendered(e);
            if (_wired) return;

            // Your XAML has: <ContentControl x:Name="MainHost" .../>
            if (FindName("MainHost") is not ContentControl host) return;

            var app = (App)System.Windows.Application.Current;
            var nav = app.Services.GetService(typeof(INavigator));
            var navTy = nav?.GetType();

            // optional sidebar (Panel OR ItemsControl)
            object? sidebar = FindName("Sidebar");
            if (sidebar is not Panel && sidebar is not ItemsControl) sidebar = null;

            // Attach host if the navigator exposes an API for it
            if (nav != null && navTy != null)
            {
                // Attach(ContentControl, <sidebar>)
                var attach2 = navTy.GetMethods().FirstOrDefault(m =>
                    m.Name.StartsWith("Attach", StringComparison.OrdinalIgnoreCase) &&
                    m.GetParameters().Length == 2 &&
                    m.GetParameters()[0].ParameterType.IsInstanceOfType(host) &&
                    sidebar != null &&
                    m.GetParameters()[1].ParameterType.IsInstanceOfType(sidebar));

                if (attach2 != null && sidebar != null)
                {
                    attach2.Invoke(nav, new[] { host, sidebar });
                }
                else
                {
                    // AttachHost/Attach/UseHost(ContentControl)
                    var attach1 = navTy.GetMethods().FirstOrDefault(m =>
                        (m.Name.Equals("AttachHost", StringComparison.OrdinalIgnoreCase) ||
                         m.Name.Equals("Attach", StringComparison.OrdinalIgnoreCase) ||
                         m.Name.Equals("UseHost", StringComparison.OrdinalIgnoreCase)) &&
                        m.GetParameters().Length == 1 &&
                        m.GetParameters()[0].ParameterType.IsInstanceOfType(host));

                    if (attach1 != null)
                    {
                        attach1.Invoke(nav, new object[] { host });
                    }
                    else
                    {
                        // Property Host/ContentHost
                        var hostProp = navTy.GetProperty("Host");
                        if (hostProp != null && hostProp.CanWrite && hostProp.PropertyType.IsInstanceOfType(host))
                        {
                            hostProp.SetValue(nav, host);
                        }
                        else
                        {
                            var contentHostProp = navTy.GetProperty("ContentHost");
                            if (contentHostProp != null && contentHostProp.CanWrite && contentHostProp.PropertyType.IsInstanceOfType(host))
                            {
                                contentHostProp.SetValue(nav, host);
                            }
                        }
                    }
                }
            }

            BuildSidebar(host);
            NavigateToRoute(host, "app://core/dashboard");

            _wired = true;
        }

        private void BuildSidebar(ContentControl host)
        {
            object? sidebar = FindName("Sidebar");
            if (sidebar is Panel panel)
            {
                panel.Children.Clear();
                foreach (var (title, route) in Routes())
                {
                    var b = MkBtn(title, () => NavigateToRoute(host, route));
                    panel.Children.Add(b);
                }
            }
            else if (sidebar is ItemsControl items)
            {
                items.Items.Clear();
                foreach (var (title, route) in Routes())
                {
                    var b = MkBtn(title, () => NavigateToRoute(host, route));
                    items.Items.Add(b);
                }
            }

            static Button MkBtn(string title, Action go)
            {
                var b = new Button
                {
                    Content = title,
                    Margin = new Thickness(8, 6, 8, 0),
                    Padding = new Thickness(10, 6, 10, 6),
                    HorizontalContentAlignment = HorizontalAlignment.Left
                };
                b.Click += (_, __) => go();
                return b;
            }
        }

        private void NavigateToRoute(ContentControl host, string route)
        {
            var app = (App)System.Windows.Application.Current;
            var nav = app.Services.GetService(typeof(INavigator));
            var navTy = nav?.GetType();

            var before = host.Content;
            var navigated = false;

            if (nav != null && navTy != null)
            {
                var navigate = navTy.GetMethod("Navigate", new[] { typeof(string) });
                if (navigate != null)
                {
                    navigate.Invoke(nav, new object[] { route });
                    navigated = true;
                }
            }

            if (!navigated || host.Content == null || ReferenceEquals(host.Content, before))
            {
                host.Content = ResolvePageForRoute(app.Services, route)
                               ?? new TextBlock { Text = $"Route not available: {route}" };
            }
        }

        private static FrameworkElement? ResolvePageForRoute(IServiceProvider sp, string route)
        {
            string[]? candidates = route switch
            {
                "app://core/dashboard" => new[] { "BestFlex.Shell.Pages.DashboardPage" },
                "app://sales/new" => new[] { "BestFlex.Shell.Pages.NewSalePage" },
                "app://sales/invoices" => new[] { "BestFlex.Shell.Pages.InvoicesPage" },
                "app://core/templates" => new[] { "BestFlex.Shell.Pages.TemplateDesignerPage" },
                "app://inventory/receive" => new[] { "BestFlex.Shell.Views.Pages.Inventory.ReceiveStockPage" },
                "app://sales/statements" => new[] { "BestFlex.Shell.Views.Pages.Sales.CustomerStatementsPage" },
                _ => null
            };
            if (candidates == null) return null;

            foreach (var fullName in candidates)
            {
                var t = FindType(fullName); // uses helper in MainWindow.Events.cs
                if (t == null) continue;

                var obj = sp.GetService(t) as FrameworkElement
                          ?? Activator.CreateInstance(t) as FrameworkElement;
                if (obj != null) return obj;
            }
            return null;
        }

        private static (string Title, string Route)[] Routes() => new[]
        {
            ("Dashboard",           "app://core/dashboard"),
            ("New Sale",            "app://sales/new"),
            ("Invoices",            "app://sales/invoices"),
            ("Templates",           "app://core/templates"),
            ("Receive Stock (GRN)", "app://inventory/receive"),
            ("Customer Statements", "app://sales/statements")
        };
    }
}
