using System.Windows;
using System.Windows.Controls;
using BestFlex.Application.Abstractions;
using BestFlex.Infrastructure.Services;
using BestFlex.Shell.ViewModels;
using Microsoft.Extensions.DependencyInjection;




namespace BestFlex.Shell
{
    public partial class ChangePasswordWindow : Window
    {
        private readonly ChangePasswordViewModel _vm;

        public ChangePasswordWindow()
        {
            InitializeComponent();
            var app = (App)System.Windows.Application.Current;
            _vm = new ChangePasswordViewModel(app.Services, app.Services.GetRequiredService<IAuditService>());
            DataContext = _vm;
            
            Loaded += (_, __) => CurrentBox.Focus();
        }


        private void CurrentBox_PasswordChanged(object sender, RoutedEventArgs e)
        {
            if (sender is PasswordBox pb) _vm.CurrentPassword = pb.Password ?? string.Empty;
        }

        private void NewBox_PasswordChanged(object sender, RoutedEventArgs e)
        {
            if (sender is PasswordBox pb) _vm.NewPassword = pb.Password ?? string.Empty;
        }

        private void ConfirmBox_PasswordChanged(object sender, RoutedEventArgs e)
        {
            if (sender is PasswordBox pb) _vm.ConfirmPassword = pb.Password ?? string.Empty;
        }

        private void Cancel_Click(object sender, RoutedEventArgs e) => DialogResult = false;
    }
}
