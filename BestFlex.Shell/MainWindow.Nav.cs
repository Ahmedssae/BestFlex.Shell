using BestFlex.Shell.Infrastructure;
using BestFlex.Shell.Navigation;
using BestFlex.Shell.Views;
using BestFlex.Shell.ViewModels;
using BestFlex.Shell.Abstractions;
using BestFlex.Shell.Pages;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
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
            NavigateToRoute((App)System.Windows.Application.Current, host, "app://core/dashboard");

            _wired = true;
        }

        private void BuildSidebar(ContentControl host)
        {
            var app = (App)System.Windows.Application.Current;
            var capabilityService = app.Services.GetRequiredService<Services.ICapabilityService>();
            
            object? sidebar = FindName("Sidebar");
            if (sidebar is Panel panel)
            {
                panel.Children.Clear();
                foreach (var (title, route) in Routes())
                {
                    var status = capabilityService.GetRouteStatus(route);
                    var b = MkCapabilityBtn(title, route, status, () => NavigateToRoute(app, host, route));
                    panel.Children.Add(b);
                }
            }
            else if (sidebar is ItemsControl items)
            {
                items.Items.Clear();
                foreach (var (title, route) in Routes())
                {
                    var status = capabilityService.GetRouteStatus(route);
                    var b = MkCapabilityBtn(title, route, status, () => NavigateToRoute(app, host, route));
                    items.Items.Add(b);
                }
            }

            static Button MkCapabilityBtn(string title, string route, Configuration.FeatureStatus status, Action go)
            {
                var stackPanel = new StackPanel
                {
                    Orientation = Orientation.Horizontal
                };

                // Status indicator
                var statusIcon = new TextBlock
                {
                    Text = status switch
                    {
                        Configuration.FeatureStatus.ProductionReady => "✅",
                        Configuration.FeatureStatus.InDevelopment => "🚧",
                        Configuration.FeatureStatus.ComingSoon => "❌",
                        _ => "❌"
                    },
                    Margin = new Thickness(0, 0, 8, 0),
                    VerticalAlignment = VerticalAlignment.Center
                };

                // Title
                var titleBlock = new TextBlock
                {
                    Text = title,
                    VerticalAlignment = VerticalAlignment.Center
                };

                // Add status suffix for non-production features
                if (status != Configuration.FeatureStatus.ProductionReady)
                {
                    var statusText = status switch
                    {
                        Configuration.FeatureStatus.InDevelopment => " 🚧 Coming Soon",
                        Configuration.FeatureStatus.ComingSoon => " 🚧 Coming Soon",
                        _ => " (Disabled)"
                    };
                    titleBlock.Text += statusText;
                }

                stackPanel.Children.Add(statusIcon);
                stackPanel.Children.Add(titleBlock);

                var b = new Button
                {
                    Content = stackPanel,
                    Margin = new Thickness(8, 4, 8, 4),  // Consistent vertical spacing
                    Padding = new Thickness(12, 8, 12, 8),  // Better padding
                    HorizontalContentAlignment = HorizontalAlignment.Left,
                    VerticalContentAlignment = VerticalAlignment.Center,
                    MinHeight = 36,  // Consistent button height
                    IsEnabled = status == Configuration.FeatureStatus.ProductionReady,
                    Opacity = status == Configuration.FeatureStatus.ProductionReady ? 1.0 : 0.6,
                    BorderThickness = new Thickness(1),
                    Background = status == Configuration.FeatureStatus.ProductionReady 
                        ? System.Windows.Media.Brushes.White 
                        : new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(245, 245, 245))
                };

                // Set tooltip based on status
                b.ToolTip = status switch
                {
                    Configuration.FeatureStatus.ProductionReady => $"{title} - Available in {Configuration.ErpCapabilityManifest.ReleaseName}",
                    Configuration.FeatureStatus.InDevelopment => $"{title} - Currently under development (Coming in v1.1)",
                    Configuration.FeatureStatus.ComingSoon => $"{title} - Planned for future release (Coming in v1.1)",
                    _ => $"{title} - Not available"
                };

                // Only add click handler for enabled buttons
                if (status == Configuration.FeatureStatus.ProductionReady)
                {
                    b.Click += (_, __) => go();
                }
                else
                {
                    // For disabled buttons, add a click handler that shows explanation
                    b.Click += (_, __) => 
                    {
                        var message = status switch
                        {
                            Configuration.FeatureStatus.InDevelopment => $"{title} is currently under development and will be available in v1.1.",
                            Configuration.FeatureStatus.ComingSoon => $"{title} is planned for a future release (v1.1+).",
                            _ => $"{title} is not available in {Configuration.ErpCapabilityManifest.ReleaseName}."
                        };
                        System.Windows.MessageBox.Show(message, "Feature Not Available", MessageBoxButton.OK, MessageBoxImage.Information);
                    };
                }
                
                return b;
            }
        }

        private static void NavigateToRoute(App app, ContentControl host, string route)
        {
            System.Diagnostics.Debug.WriteLine($"[NAV] Route requested: {route}");

            if (RouteRegistry.TryResolve(route, out var page))
            {
                host.Content = page;
                System.Diagnostics.Debug.WriteLine($"[NAV] Route loaded: {route}");
                return;
            }

            host.Content = new SafeFallbackPage(route);
            System.Diagnostics.Debug.WriteLine($"[NAV] Route failed, fallback shown: {route}");
        }

        private static void NavigateToDashboardExplicit(App app, ContentControl host)
        {
            try
            {
                System.Diagnostics.Debug.WriteLine($"[DEBUG] NavigateToDashboardExplicit called. Host type: {host?.GetType().Name}, Host is null: {host == null}");
                
                // Create simple dashboard without theme dependencies (permanent solution)
                var dashboard = new System.Windows.Controls.StackPanel
                {
                    Background = System.Windows.Media.Brushes.White,
                    Margin = new System.Windows.Thickness(20)
                };
                
                // Header
                dashboard.Children.Add(new System.Windows.Controls.TextBlock
                {
                    Text = "🎯 BestFlex ERP v1.0 Dashboard",
                    FontSize = 24,
                    FontWeight = System.Windows.FontWeights.Bold,
                    Foreground = System.Windows.Media.Brushes.DarkBlue,
                    Margin = new System.Windows.Thickness(0, 0, 0, 20)
                });
                
                // Status Banner
                var statusBorder = new System.Windows.Controls.Border
                {
                    Background = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(240, 240, 240)),
                    Margin = new System.Windows.Thickness(0, 0, 0, 20),
                    Padding = new System.Windows.Thickness(16)
                };
                
                var statusPanel = new System.Windows.Controls.StackPanel();
                statusPanel.Children.Add(new System.Windows.Controls.TextBlock
                {
                    Text = "🎯 BestFlex ERP v1.0",
                    FontSize = 18,
                    FontWeight = System.Windows.FontWeights.Bold,
                    Foreground = System.Windows.Media.Brushes.DarkBlue,
                    Margin = new System.Windows.Thickness(0, 0, 0, 8)
                });
                
                statusPanel.Children.Add(new System.Windows.Controls.TextBlock
                {
                    Text = "✅ Production-Ready Features: Login, Users, Customers, Products, Sales Orders, Invoices, Inventory Visibility",
                    FontSize = 12,
                    Foreground = System.Windows.Media.Brushes.Green,
                    Margin = new System.Windows.Thickness(0, 0, 0, 4)
                });
                
                statusPanel.Children.Add(new System.Windows.Controls.TextBlock
                {
                    Text = "🚧 In Development (v1.1+): Advanced Dashboard Features, Receive Stock, Templates",
                    FontSize = 12,
                    Foreground = System.Windows.Media.Brushes.Orange,
                    Margin = new System.Windows.Thickness(0, 0, 0, 4)
                });
                
                statusPanel.Children.Add(new System.Windows.Controls.TextBlock
                {
                    Text = "❌ Coming Soon: Reports, Analytics, Advanced Features",
                    FontSize = 12,
                    Foreground = System.Windows.Media.Brushes.Gray
                });
                
                statusBorder.Child = statusPanel;
                dashboard.Children.Add(statusBorder);
                
                // Inventory Status Card
                var inventoryCard = CreateDashboardCard("📦 Inventory Status", "Inventory visibility is available in v1.0", "Advanced inventory management coming in v1.1+");
                dashboard.Children.Add(inventoryCard);
                
                // Sales & Invoicing Card
                var salesCard = CreateDashboardCard("💰 Sales & Invoicing", 
                    "✅ Sales Orders - Create and validate orders\n✅ Invoices - Post and view invoices", 
                    "❌ Customer Statements - Coming in v1.1+");
                dashboard.Children.Add(salesCard);
                
                // Put it in a ScrollViewer
                var scrollViewer = new System.Windows.Controls.ScrollViewer
                {
                    Content = dashboard,
                    VerticalScrollBarVisibility = System.Windows.Controls.ScrollBarVisibility.Auto
                };
                
                System.Diagnostics.Debug.WriteLine($"[DEBUG] Dashboard created successfully");
                
                // Update the content host directly
                var beforeContent = host?.Content;
                host!.Content = scrollViewer;
                var afterContent = host.Content;
                
                System.Diagnostics.Debug.WriteLine($"[DEBUG] Content assignment. Before: {beforeContent?.GetType().Name}, After: {afterContent?.GetType().Name}");
                System.Diagnostics.Debug.WriteLine($"[DEBUG] Content changed: {!ReferenceEquals(beforeContent, afterContent)}");
                
                // Force UI update
                scrollViewer.UpdateLayout();
                scrollViewer.InvalidateVisual();
                
                var logger = app.Services.GetService<Microsoft.Extensions.Logging.ILogger<MainWindow>>();
                logger?.LogInformation("Dashboard navigation completed successfully");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[DEBUG] Exception in NavigateToDashboardExplicit: {ex}");
                var logger = app.Services.GetService<Microsoft.Extensions.Logging.ILogger<MainWindow>>();
                logger?.LogError(ex, "Failed to create dashboard page explicitly");
                
                // Last resort fallback
                var fallbackPage = new BestFlex.Shell.Pages.SafeFallbackPage("Dashboard temporarily unavailable");
                host!.Content = fallbackPage;
            }
        }

        private static System.Windows.Controls.Border CreateDashboardCard(string title, string content, string footer = "")
        {
            var border = new System.Windows.Controls.Border
            {
                Background = System.Windows.Media.Brushes.White,
                BorderBrush = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(229, 231, 235)),
                BorderThickness = new System.Windows.Thickness(1),
                CornerRadius = new System.Windows.CornerRadius(8),
                Margin = new System.Windows.Thickness(0, 0, 0, 20),
                Padding = new System.Windows.Thickness(16)
            };
            
            var panel = new System.Windows.Controls.StackPanel();
            
            panel.Children.Add(new System.Windows.Controls.TextBlock
            {
                Text = title,
                FontWeight = System.Windows.FontWeights.SemiBold,
                Foreground = System.Windows.Media.Brushes.Black,
                Margin = new System.Windows.Thickness(0, 0, 0, 10)
            });
            
            panel.Children.Add(new System.Windows.Controls.TextBlock
            {
                Text = content,
                FontSize = 12,
                Foreground = System.Windows.Media.Brushes.Green,
                Margin = new System.Windows.Thickness(0, 0, 0, 10)
            });
            
            if (!string.IsNullOrEmpty(footer))
            {
                panel.Children.Add(new System.Windows.Controls.TextBlock
                {
                    Text = footer,
                    FontSize = 12,
                    Foreground = System.Windows.Media.Brushes.Gray,
                    TextWrapping = System.Windows.TextWrapping.Wrap
                });
            }
            
            border.Child = panel;
            return border;
        }

        private static void ShowCapabilityFallback(App app, ContentControl host, string route, Configuration.FeatureStatus status)
        {
            var capabilityService = app.Services.GetRequiredService<Services.ICapabilityService>();
            var logger = app.Services.GetRequiredService<ILogger<ViewModels.SafeFallbackViewModel>>();
            var navigationService = app.Services.GetRequiredService<Abstractions.IShellNavigationService>();

            var fallbackVm = new ViewModels.SafeFallbackViewModel(logger, app.Services, navigationService);

            // Set message based on capability status
            fallbackVm.Message = status switch
            {
                Configuration.FeatureStatus.InDevelopment => $"🚧 Feature In Development\n\n{GetFeatureName(route)} is currently under development and will be available in a future release (v1.1+).",
                Configuration.FeatureStatus.ComingSoon => $"❌ Feature Coming Soon\n\n{GetFeatureName(route)} is planned for a future release but not yet available.",
                _ => $"Feature Not Available\n\n{GetFeatureName(route)} is not available in {Configuration.ErpCapabilityManifest.ReleaseName}."
            };

            var fallbackView = new Views.SafeFallbackView();
            fallbackView.DataContext = fallbackVm;
            host.Content = fallbackView;
        }

        private static void ShowSafeFallback(App app, ContentControl host, string route)
        {
            try
            {
                // PURE UI FALLBACK: No navigation dependencies
                var logger = app.Services.GetService<ILogger<ViewModels.SafeFallbackViewModel>>();
                
                // ROUTE-AWARE: Pass the failed route to get proper messaging
                var fallbackVm = new ViewModels.SafeFallbackViewModel(route);

                var fallbackView = new Views.SafeFallbackView();
                fallbackView.DataContext = fallbackVm;
                host.Content = fallbackView;
            }
            catch
            {
                // ULTIMATE FALLBACK: Even SafeFallbackView failed - show static error
                var featureName = GetFeatureName(route);
                host.Content = new System.Windows.Controls.TextBlock
                {
                    Text = $"⚠️ {featureName} is temporarily unavailable.\n\nPlease restart the application.",
                    FontSize = 16,
                    FontWeight = System.Windows.FontWeights.Bold,
                    HorizontalAlignment = System.Windows.HorizontalAlignment.Center,
                    VerticalAlignment = System.Windows.VerticalAlignment.Center,
                    Margin = new System.Windows.Thickness(20),
                    TextWrapping = System.Windows.TextWrapping.Wrap
                };
            }
        }

        private static string GetFeatureName(string route)
        {
            return route switch
            {
                "app://core/dashboard" => "Dashboard",
                "app://sales/new" => "Sales Orders",
                "app://sales/invoices" => "Invoices",
                "app://sales/statements" => "Customer Statements",
                "app://inventory/receive" => "Receive Stock",
                "app://core/templates" => "Templates",
                _ => "This feature"
            };
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
                var t = FindType(fullName);
                if (t == null) continue;

                // Try to create with dependency injection first
                try
                {
                    var obj = sp.GetRequiredService(t) as FrameworkElement;
                    if (obj != null) return obj;
                }
                catch
                {
                    // If dependency injection fails, try safe constructor for NewSalePage
                    if (fullName == "BestFlex.Shell.Pages.NewSalePage")
                    {
                        try
                        {
                            var safePage = Activator.CreateInstance(t) as FrameworkElement;
                            if (safePage != null) return safePage;
                        }
                        catch
                        {
                            // Safe constructor also failed, continue to next candidate
                        }
                    }
                }
            }

            return null;
        }

        private static Type? FindType(string fullName)
        {
            foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
            {
                var t = asm.GetType(fullName, throwOnError: false);
                if (t != null) return t;
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
