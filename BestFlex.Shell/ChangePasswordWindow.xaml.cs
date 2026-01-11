using System.Windows;
using BestFlex.Application.Abstractions; // ICurrentUserService
using BestFlex.Infrastructure.Services; // PasswordService
using Microsoft.Extensions.DependencyInjection;




namespace BestFlex.Shell
{
    public partial class ChangePasswordWindow : Window
    {
        private readonly PasswordService _passwords;
        private readonly ICurrentUserService _current;

        public ChangePasswordWindow()
        {
            InitializeComponent();
            var app = (App)System.Windows.Application.Current;   // ✅ fully qualified
            _passwords = app.Services.GetRequiredService<PasswordService>();
            _current = app.Services.GetRequiredService<ICurrentUserService>();
            Loaded += (_, __) => CurrentBox.Focus();
        }


        private async void Save_Click(object sender, RoutedEventArgs e)
        {
            ErrorText.Visibility = Visibility.Collapsed;

            var curr = CurrentBox.Password ?? "";
            var next = NewBox.Password ?? "";

            if (string.IsNullOrWhiteSpace(curr) || string.IsNullOrWhiteSpace(next))
            {
                ShowError("Enter both current and new passwords.");
                return;
            }
            if (next.Length < 6) // simple policy; tweak later
            {
                ShowError("New password must be at least 6 characters.");
                return;
            }

            var ok = await _passwords.ChangePasswordAsync(_current.UserId, curr, next);
            if (!ok)
            {
                ShowError("Current password is incorrect.");
                CurrentBox.Clear();
                CurrentBox.Focus();
                return;
            }

            MessageBox.Show("Password changed successfully.", "BestFlex",
                MessageBoxButton.OK, MessageBoxImage.Information);
            DialogResult = true;
        }

        private void Cancel_Click(object sender, RoutedEventArgs e) => DialogResult = false;

        private void ShowError(string msg)
        {
            ErrorText.Text = msg;
            ErrorText.Visibility = Visibility.Visible;
        }
    }
}
