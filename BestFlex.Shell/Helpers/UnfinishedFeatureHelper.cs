using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace BestFlex.Shell.Helpers
{
    /// <summary>
    /// Helper class for managing unfinished features with honest UX messaging
    /// Implements the "Unfinished ≠ Broken" principle
    /// </summary>
    public static class UnfinishedFeatureHelper
    {
        /// <summary>
        /// Shows an honest message about an unfinished feature
        /// </summary>
        /// <param name="featureName">Name of the feature</param>
        /// <param name="additionalInfo">Additional information about the feature</param>
        public static void ShowUnfinishedFeatureMessage(string featureName, string additionalInfo = "")
        {
            var message = $"{featureName} is not fully implemented yet.\n\nThis feature is under development.";
            if (!string.IsNullOrWhiteSpace(additionalInfo))
            {
                message += $"\n\n{additionalInfo}";
            }
            message += "\n\nPlease check back in a future release.";

            MessageBox.Show(message, "BestFlex - Feature Under Development", 
                MessageBoxButton.OK, MessageBoxImage.Information);
        }

        /// <summary>
        /// Creates an overlay for a control to indicate the feature is unfinished
        /// </summary>
        /// <param name="parentControl">The control to overlay</param>
        /// <param name="message">Message to display</param>
        /// <returns>The overlay grid</returns>
        public static Grid CreateUnfinishedFeatureOverlay(FrameworkElement parentControl, string message)
        {
            var overlay = new Grid
            {
                Background = new SolidColorBrush(Color.FromArgb(200, 240, 240, 240)),
                IsHitTestVisible = true
            };

            // Add semi-transparent border
            var border = new Border
            {
                Background = Brushes.White,
                CornerRadius = new CornerRadius(8),
                Margin = new Thickness(20),
                Padding = new Thickness(20),
                BorderBrush = new SolidColorBrush(Color.FromRgb(200, 200, 200)),
                BorderThickness = new Thickness(1)
            };

            var stackPanel = new StackPanel
            {
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center
            };

            // Add icon
            var icon = new TextBlock
            {
                Text = "🚧",
                FontSize = 48,
                HorizontalAlignment = HorizontalAlignment.Center,
                Margin = new Thickness(0, 0, 0, 10)
            };

            // Add title
            var title = new TextBlock
            {
                Text = "Feature Under Development",
                FontSize = 18,
                FontWeight = FontWeights.Bold,
                HorizontalAlignment = HorizontalAlignment.Center,
                Margin = new Thickness(0, 0, 0, 10),
                Foreground = new SolidColorBrush(Color.FromRgb(100, 100, 100))
            };

            // Add message
            var messageBlock = new TextBlock
            {
                Text = message,
                FontSize = 14,
                TextWrapping = TextWrapping.Wrap,
                HorizontalAlignment = HorizontalAlignment.Center,
                Foreground = new SolidColorBrush(Color.FromRgb(80, 80, 80)),
                MaxWidth = 400
            };

            stackPanel.Children.Add(icon);
            stackPanel.Children.Add(title);
            stackPanel.Children.Add(messageBlock);

            border.Child = stackPanel;
            overlay.Children.Add(border);

            return overlay;
        }

        /// <summary>
        /// Disables a control and adds a tooltip indicating it's unfinished
        /// </summary>
        /// <param name="control">The control to disable</param>
        /// <param name="featureName">Name of the unfinished feature</param>
        public static void DisableForUnfinishedFeature(FrameworkElement control, string featureName)
        {
            if (control == null) return;

            control.IsEnabled = false;
            control.Opacity = 0.6;
            control.ToolTip = $"{featureName}\n\nThis feature is under development and not yet available.";
        }

        /// <summary>
        /// Shows a non-blocking notification about an unfinished feature
        /// </summary>
        /// <param name="featureName">Name of the feature</param>
        /// <param name="action">The action that was attempted</param>
        public static void ShowUnfinishedFeatureNotification(string featureName, string action)
        {
            var message = $"Cannot {action} - {featureName} is not fully implemented yet.\n\nThis feature is under development.";
            
            // Create a simple notification (in a real app, this could be a toast notification)
            MessageBox.Show(message, "BestFlex - Feature Under Development", 
                MessageBoxButton.OK, MessageBoxImage.Information);
        }

        /// <summary>
        /// Checks if a feature is implemented and throws NotImplementedException if not
        /// </summary>
        /// <param name="featureName">Name of the feature</param>
        /// <param name="isImplemented">Whether the feature is implemented</param>
        public static void EnsureFeatureImplemented(string featureName, bool isImplemented)
        {
            if (!isImplemented)
            {
                throw new NotImplementedException($"{featureName} is not fully implemented yet.");
            }
        }

        /// <summary>
        /// Creates a "Coming Soon" banner for a feature
        /// </summary>
        /// <param name="featureName">Name of the feature</param>
        /// <returns>A banner control</returns>
        public static Border CreateComingSoonBanner(string featureName)
        {
            var banner = new Border
            {
                Background = new SolidColorBrush(Color.FromRgb(255, 248, 220)),
                BorderBrush = new SolidColorBrush(Color.FromRgb(255, 193, 7)),
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(4),
                Padding = new Thickness(10, 5, 10, 5),
                Margin = new Thickness(0, 0, 0, 10)
            };

            var stackPanel = new StackPanel
            {
                Orientation = Orientation.Horizontal
            };

            var icon = new TextBlock
            {
                Text = "🔜",
                FontSize = 14,
                Margin = new Thickness(0, 0, 5, 0),
                VerticalAlignment = VerticalAlignment.Center
            };

            var text = new TextBlock
            {
                Text = $"{featureName} - Coming Soon",
                FontSize = 12,
                FontWeight = FontWeights.SemiBold,
                Foreground = new SolidColorBrush(Color.FromRgb(180, 120, 0)),
                VerticalAlignment = VerticalAlignment.Center
            };

            stackPanel.Children.Add(icon);
            stackPanel.Children.Add(text);
            banner.Child = stackPanel;

            return banner;
        }
    }
}
