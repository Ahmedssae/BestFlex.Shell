using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using BestFlex.Shell.Pages;

namespace BestFlex.Shell
{
    public partial class MainWindow : Window
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

        public void InitializeShell()
        {
            try
            {
                _logger?.LogInformation("MainWindow.InitializeShell started");
                
                // Get the ViewModel through DI - this will properly initialize with feature checks
                var app = (App)System.Windows.Application.Current;
                var vm = app.Services.GetRequiredService<BestFlex.Shell.ViewModels.MainWindowViewModel>();
                
                // Check for unavailable CORE features only - this is the only place we should ever block startup
                var unavailableCoreFeatures = vm.GetUnavailableCoreFeatures();
                if (unavailableCoreFeatures.Any())
                {
                    var featureNames = string.Join(", ", unavailableCoreFeatures.Select(f => f.Name));
                    var message = $"Core ERP features unavailable: {featureNames}. Application cannot continue.";
                    _logger?.LogCritical("Core features unavailable: {FeatureNames}", featureNames);
                    MessageBox.Show(message, "Critical Error", MessageBoxButton.OK, MessageBoxImage.Error);
                    Close();
                    return;
                }

                // Load ViewModel data - this will check its own feature availability
                vm.LoadAsync().GetAwaiter().GetResult();
                
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
            }
        }

        public void NavigateToInitialPage()
        {
            try
            {
                _logger?.LogInformation("MainWindow.NavigateToInitialPage started");
                
                var app = (App)System.Windows.Application.Current;
                var navigationService = app.Services.GetService<BestFlex.Shell.Abstractions.IShellNavigationService>();
                
                if (navigationService != null)
                {
                    navigationService.NavigateToDashboard();
                    _logger?.LogInformation("MainWindow.NavigateToInitialPage completed successfully");
                }
                else
                {
                    throw new InvalidOperationException("Navigation service not available");
                }
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Failed to navigate to initial page");
                // Show fallback page directly
                MainHost.Content = new SafeFallbackPage($"Navigation failed: {ex.Message}");
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