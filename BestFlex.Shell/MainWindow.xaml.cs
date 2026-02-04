using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using BestFlex.Shell.Pages;
using BestFlex.Shell.Abstractions;

namespace BestFlex.Shell
{
    public partial class MainWindow : Window, IMainContentHost
    {
        private readonly ILogger<MainWindow>? _logger;

        public MainWindow()
        {
            InitializeComponent();

            try
            {
                var app = (App)System.Windows.Application.Current;
                _logger = app.Services.GetRequiredService<ILogger<MainWindow>>();
                
                // Set DataContext explicitly
                DataContext = this;
                
                _logger?.LogInformation("Shell initialized successfully");
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "MainWindow initialization failed");
                MessageBox.Show($"Failed to initialize main window: {ex.Message}", "Error", 
                    MessageBoxButton.OK, MessageBoxImage.Error);
                Close();
            }
        }

        // SINGLE CONTENT HOST CONTRACT
        public void Show(object view)
        {
            // Find the content host (second child of main grid)
            if (Content is Grid mainGrid && mainGrid.Children.Count > 1)
            {
                var contentHost = mainGrid.Children[1] as ContentControl;
                if (contentHost != null)
                {
                    contentHost.Content = view;
                    return;
                }
            }
            
            throw new InvalidOperationException("Content host not found in MainWindow");
        }

        public async Task InitializeShell()
        {
            try
            {
                _logger?.LogInformation("MainWindow.InitializeShell started");
                
                // Get the ViewModel through DI - this will properly initialize with feature checks
                var app = (App)System.Windows.Application.Current;
                var vm = app.Services.GetRequiredService<BestFlex.Shell.ViewModels.MainWindowViewModel>();
                
                // Log available features for debugging but do NOT block startup
                var unavailableCoreFeatures = await vm.GetUnavailableCoreFeatures();
                if (unavailableCoreFeatures.Any())
                {
                    var featureNames = string.Join(", ", unavailableCoreFeatures);
                    _logger?.LogWarning("Some features not available: {FeatureNames}", featureNames);
                    // DO NOT block startup - ERP must be resilient
                }

                // Load ViewModel data - this will check its own feature availability
                try
                {
                    await vm.LoadAsync();
                }
                catch (Exception loadEx)
                {
                    _logger?.LogError(loadEx, "Failed to load ViewModel data during initialization");
                    // Continue initialization even if VM loading fails
                }
                
                // Hide template entries if not admin
                if (!vm.IsAdmin)
                {
                    HideTemplateEntries();
                }
                
                _logger?.LogInformation("MainWindow.InitializeShell completed successfully");
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Failed to initialize shell");
                MessageBox.Show($"Failed to initialize shell: {ex.Message}", "Error", 
                    MessageBoxButton.OK, MessageBoxImage.Error);
                // Re-throw to allow caller to handle the failure
                throw;
            }
        }

        public void NavigateToInitialPage()
        {
            // EXPLICIT NAVIGATION: No routes, no strings, no fallbacks
            try
            {
                _logger?.LogInformation("MainWindow.NavigateToInitialPage started");
                
                var app = (App)System.Windows.Application.Current;
                var viewFactory = app.Services.GetRequiredService<BestFlex.Shell.Factories.ViewFactory>();
                
                // DETERMINISTIC: Direct factory call
                this.Show(viewFactory.CreateDashboard());
                
                _logger?.LogInformation("MainWindow.NavigateToInitialPage completed successfully");
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Failed to navigate to initial page");
                
                // TEMPORARY: Show simple fallback instead of crashing
                try
                {
                    var fallbackText = new TextBlock 
                    { 
                        Text = "Application loading... Please wait.",
                        FontSize = 16,
                        HorizontalAlignment = HorizontalAlignment.Center,
                        VerticalAlignment = VerticalAlignment.Center,
                        Margin = new Thickness(20)
                    };
                    
                    if (Content is Grid mainGrid && mainGrid.Children.Count > 1)
                    {
                        var contentHost = mainGrid.Children[1] as ContentControl;
                        if (contentHost != null)
                        {
                            contentHost.Content = fallbackText;
                        }
                    }
                }
                catch
                {
                    // Last resort - do nothing
                }
            }
        }

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
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Failed to hide template entries");
                // best-effort; never break shell
            }
        }

        private static IEnumerable<T> EnumerateVisuals<T>(DependencyObject root) where T : DependencyObject
        {
            var stack = new Stack<DependencyObject>();
            stack.Push(root);

            while (stack.Count > 0)
            {
                var current = stack.Pop();
                if (current is T item)
                    yield return item;

                var childrenCount = VisualTreeHelper.GetChildrenCount(current);
                for (int i = 0; i < childrenCount; i++)
                {
                    var child = VisualTreeHelper.GetChild(current, i);
                    stack.Push(child);
                }
            }
        }
    }
}