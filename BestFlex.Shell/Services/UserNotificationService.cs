using System;
using System.Windows;
using BestFlex.Application.Abstractions;
using Microsoft.Extensions.Logging;

namespace BestFlex.Shell.Services
{
    public sealed class UserNotificationService : IUserNotificationService
    {
        private readonly ILogger<UserNotificationService> _logger;

        public UserNotificationService(ILogger<UserNotificationService> logger)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public void ShowError(string message)
        {
            try
            {
                _logger.LogWarning("Showing error to user: {Message}", message);
                MessageBox.Show(
                    System.Windows.Application.Current?.MainWindow,
                    message,
                    "Error",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error
                );
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to show error message: {Message}", message);
            }
        }

        public void ShowWarning(string message)
        {
            try
            {
                _logger.LogInformation("Showing warning to user: {Message}", message);
                MessageBox.Show(
                    System.Windows.Application.Current?.MainWindow,
                    message,
                    "Warning",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning
                );
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to show warning message: {Message}", message);
            }
        }

        public void ShowInfo(string message)
        {
            try
            {
                _logger.LogInformation("Showing info to user: {Message}", message);
                MessageBox.Show(
                    System.Windows.Application.Current?.MainWindow,
                    message,
                    "Information",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information
                );
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to show info message: {Message}", message);
            }
        }
    }
}
