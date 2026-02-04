using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using BestFlex.Shell.Configuration;

namespace BestFlex.Shell.Helpers
{
    /// <summary>
    /// Helper for classifying and styling screens based on their capability status
    /// </summary>
    public static class ScreenClassificationHelper
    {
        /// <summary>
        /// Adds a status banner to a screen based on its capability
        /// </summary>
        public static Border AddStatusBanner(FrameworkElement screen, FeatureStatus status, string featureName)
        {
            var banner = status switch
            {
                FeatureStatus.ProductionReady => CreateProductionBanner(featureName),
                FeatureStatus.InDevelopment => CreateDevelopmentBanner(featureName),
                FeatureStatus.ComingSoon => CreateComingSoonBanner(featureName),
                _ => CreateDisabledBanner(featureName)
            };

            // Find the parent container and add banner at the top
            if (screen.Parent is Panel parentPanel)
            {
                parentPanel.Children.Insert(0, banner);
            }
            else if (screen.Parent is ContentControl contentControl)
            {
                var stackPanel = new StackPanel();
                stackPanel.Children.Add(banner);
                stackPanel.Children.Add(screen);
                contentControl.Content = stackPanel;
            }

            return banner;
        }

        /// <summary>
        /// Creates a production-ready banner (subtle, minimal)
        /// </summary>
        private static Border CreateProductionBanner(string featureName)
        {
            return new Border
            {
                Background = System.Windows.Media.Brushes.Transparent,
                Margin = new Thickness(0, 0, 0, 10),
                Child = new TextBlock
                {
                    Text = $"✅ {featureName} - Production Ready",
                    FontSize = 10,
                    Foreground = System.Windows.Media.Brushes.Green,
                    Margin = new Thickness(5, 2, 5, 2)
                }
            };
        }

        /// <summary>
        /// Creates an in-development banner
        /// </summary>
        private static Border CreateDevelopmentBanner(string featureName)
        {
            return new Border
            {
                Background = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(255, 248, 220)),
                BorderBrush = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(255, 193, 7)),
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(4),
                Padding = new Thickness(10, 5, 10, 5),
                Margin = new Thickness(0, 0, 0, 10),
                Child = new StackPanel
                {
                    Orientation = Orientation.Horizontal,
                    Children =
                    {
                        new TextBlock
                        {
                            Text = "🚧",
                            FontSize = 14,
                            Margin = new Thickness(0, 0, 5, 0),
                            VerticalAlignment = VerticalAlignment.Center
                        },
                        new TextBlock
                        {
                            Text = $"{featureName} - In Development (v1.1+)",
                            FontSize = 12,
                            FontWeight = FontWeights.SemiBold,
                            Foreground = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(180, 120, 0)),
                            VerticalAlignment = VerticalAlignment.Center
                        }
                    }
                }
            };
        }

        /// <summary>
        /// Creates a coming soon banner
        /// </summary>
        private static Border CreateComingSoonBanner(string featureName)
        {
            return new Border
            {
                Background = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(240, 240, 240)),
                BorderBrush = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(150, 150, 150)),
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(4),
                Padding = new Thickness(10, 5, 10, 5),
                Margin = new Thickness(0, 0, 0, 10),
                Child = new StackPanel
                {
                    Orientation = Orientation.Horizontal,
                    Children =
                    {
                        new TextBlock
                        {
                            Text = "❌",
                            FontSize = 14,
                            Margin = new Thickness(0, 0, 5, 0),
                            VerticalAlignment = VerticalAlignment.Center
                        },
                        new TextBlock
                        {
                            Text = $"{featureName} - Coming Soon",
                            FontSize = 12,
                            FontWeight = FontWeights.SemiBold,
                            Foreground = System.Windows.Media.Brushes.Gray,
                            VerticalAlignment = VerticalAlignment.Center
                        }
                    }
                }
            };
        }

        /// <summary>
        /// Creates a disabled banner
        /// </summary>
        private static Border CreateDisabledBanner(string featureName)
        {
            return new Border
            {
                Background = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(255, 240, 240)),
                BorderBrush = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(200, 50, 50)),
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(4),
                Padding = new Thickness(10, 5, 10, 5),
                Margin = new Thickness(0, 0, 0, 10),
                Child = new StackPanel
                {
                    Orientation = Orientation.Horizontal,
                    Children =
                    {
                        new TextBlock
                        {
                            Text = "🚫",
                            FontSize = 14,
                            Margin = new Thickness(0, 0, 5, 0),
                            VerticalAlignment = VerticalAlignment.Center
                        },
                        new TextBlock
                        {
                            Text = $"{featureName} - Disabled",
                            FontSize = 12,
                            FontWeight = FontWeights.SemiBold,
                            Foreground = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(150, 50, 50)),
                            VerticalAlignment = VerticalAlignment.Center
                        }
                    }
                }
            };
        }

        /// <summary>
        /// Disables all interactive controls in a container
        /// </summary>
        public static void DisableControls(DependencyObject container)
        {
            if (container is FrameworkElement element)
            {
                element.IsEnabled = false;
                element.Opacity = 0.6;
            }

            // Recursively disable child controls
            for (int i = 0; i < VisualTreeHelper.GetChildrenCount(container); i++)
            {
                var child = VisualTreeHelper.GetChild(container, i);
                DisableControls(child);
            }
        }

        /// <summary>
        /// Gets the status text for a feature
        /// </summary>
        public static string GetStatusText(FeatureStatus status, string featureName)
        {
            return status switch
            {
                FeatureStatus.ProductionReady => $"{featureName} is available in {ErpCapabilityManifest.ReleaseName}",
                FeatureStatus.InDevelopment => $"{featureName} is currently under development and will be available in v1.1+",
                FeatureStatus.ComingSoon => $"{featureName} is planned for a future release",
                _ => $"{featureName} is not available"
            };
        }
    }
}
