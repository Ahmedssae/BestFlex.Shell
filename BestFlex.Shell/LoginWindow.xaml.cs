using System.Windows;
using System.Windows.Input;
using BestFlex.Shell.ViewModels;
using Microsoft.Extensions.DependencyInjection;
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
            viewModel.PropertyChanged += (_, e) =>
            {
                if (e.PropertyName == nameof(LoginViewModel.LoginSucceeded) && viewModel.LoginSucceeded)
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
                            // Close any existing MainWindow before creating new one
                            if (System.Windows.Application.Current.MainWindow is MainWindow existingMainWindow && 
                                existingMainWindow != app.MainWindow)
                            {
                                existingMainWindow.Close();
                            }
                            
                            var mainWindow = app.Services.GetRequiredService<MainWindow>();
                            System.Windows.Application.Current.MainWindow = mainWindow;
                            
                            mainWindow.Show();
                            
                            Hide();
                            Close();
                            
                            System.Windows.Application.Current.ShutdownMode = ShutdownMode.OnMainWindowClose;
                        }
                    }
                    catch
                    {
                        _isLoginInProgress = false;
                        /* best-effort: avoid throwing from UI thread */
                    }
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
