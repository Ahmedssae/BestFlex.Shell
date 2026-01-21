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
        public LoginWindow(LoginViewModel viewModel)
        {
            InitializeComponent();
            DataContext = viewModel;
            // Listen for login success (ViewModel signals state change). Window handles showing main window.
            viewModel.PropertyChanged += (_, e) =>
            {
                if (e.PropertyName == nameof(LoginViewModel.LoginSucceeded) && viewModel.LoginSucceeded)
                {
                    try
                    {
                        var app = System.Windows.Application.Current as App;
                        if (app != null)
                        {
                            var main = app.Services.GetRequiredService<MainWindow>();
                            if (main != null)
                            {
                                app.MainWindow = main;
                                main.Show();
                                Close();
                            }
                        }
                    }
                    catch { /* best-effort: avoid throwing from UI thread */ }
                }
                // CancelRequested removed from VM; Cancel should be handled by UI (Cancel button click closes window).
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
