using System.Windows;
using System.Windows.Input;
using BestFlex.Shell.ViewModels;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using System.Windows.Controls;
using System.ComponentModel;

namespace BestFlex.Shell
{
    public partial class LoginWindow : Window
    {
        private static bool _isLoginInProgress = false;
        
        public LoginWindow(LoginViewModel viewModel)
        {
            InitializeComponent();
            DataContext = viewModel;
            
            // Listen for login success (ViewModel signals state change). Window handles showing main window.
            viewModel.LoginSucceeded += async () =>
            {
                // SINGLE-SHELL GUARANTEE - Prevent double-login execution
                if (_isLoginInProgress)
                {
                    return;
                }
                
                _isLoginInProgress = true;
                
                try
                {
                    var app = System.Windows.Application.Current as App;
                    if (app != null)
                    {
                        // Perform startup safety checks before allowing login
                        var startupSafety = app.Services.GetService<BestFlex.Infrastructure.Services.IStartupSafetyService>();
                        if (startupSafety != null)
                        {
                            var safetyResult = await startupSafety.PerformSafetyChecksAsync();
                            if (!safetyResult.IsSafe)
                            {
                                var errorMessage = $"System is not ready for login.\n\n{safetyResult.GetSummaryMessage()}\n\nError ID: {safetyResult.CorrelationId}\n\nPlease contact your administrator.";
                                MessageBox.Show(errorMessage, "System Not Ready", MessageBoxButton.OK, MessageBoxImage.Error);
                                _isLoginInProgress = false;
                                return;
                            }
                            
                            if (safetyResult.HasWarnings)
                            {
                                var warningMessage = $"System is ready but has warnings:\n\n{string.Join("\n", safetyResult.Warnings)}\n\nProceed with login?";
                                var result = MessageBox.Show(warningMessage, "System Warnings", MessageBoxButton.YesNo, MessageBoxImage.Warning);
                                if (result != MessageBoxResult.Yes)
                                {
                                    _isLoginInProgress = false;
                                    return;
                                }
                            }
                        }

                        // Close any existing MainWindow before creating new one
                        if (System.Windows.Application.Current.MainWindow is MainWindow existingMainWindow && 
                            existingMainWindow != app.MainWindow)
                        {
                            existingMainWindow.Close();
                        }
                        
                        var mainWindow = app.Services.GetRequiredService<MainWindow>();
                        System.Windows.Application.Current.MainWindow = mainWindow;
                        
                        // Initialize shell safely with proper error handling
                        try
                        {
                            await mainWindow.InitializeShell();
                            await Task.Delay(100); // Brief pause for UI stability
                            mainWindow.NavigateToInitialPage();
                        }
                        catch (Exception initEx)
                        {
                            var logger = app.Services.GetService<Microsoft.Extensions.Logging.ILogger<App>>();
                            logger?.LogError(initEx, "Failed to initialize main window shell");
                            MessageBox.Show($"Failed to initialize application: {initEx.Message}", "Startup Error", 
                                MessageBoxButton.OK, MessageBoxImage.Error);
                            return;
                        }
                        
                        mainWindow.Show();
                        
                        Hide();
                        Close();
                        
                        System.Windows.Application.Current.ShutdownMode = ShutdownMode.OnMainWindowClose;
                    }
                }
                catch (Exception ex)
                {
                    _isLoginInProgress = false;
                    var app = System.Windows.Application.Current as App;
                    var logger = app?.Services?.GetService<Microsoft.Extensions.Logging.ILogger<App>>();
                    logger?.LogError(ex, "Critical error during login success handling");
                    MessageBox.Show($"Failed to start application: {ex.Message}", "Critical Error", 
                        MessageBoxButton.OK, MessageBoxImage.Error);
                }
            };
        }

        // Removed older DialogResult mirroring; window listens directly to LoginSucceeded/CancelRequested.

        private void PasswordBox_OnPasswordChanged(object sender, RoutedEventArgs e)
        {
            if (DataContext is LoginViewModel vm && sender is PasswordBox pb)
            {
                vm.Password = pb.Password;
            }
        }

        private void Cancel_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }
    }
}
