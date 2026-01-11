using System.Windows;
using BestFlex.Application.Abstractions;   // ICurrentUserService, IUserRepository
using BestFlex.Infrastructure.Services;

using Microsoft.Extensions.DependencyInjection;

namespace BestFlex.Shell
{
    public partial class LoginWindow : Window
    {
        private readonly LoginService _login;
        private readonly IUserRepository _users;
        private readonly ICurrentUserService _currentUser;

        public LoginWindow()
        {
            InitializeComponent();

            // IMPORTANT: explicitly use System.Windows.Application to avoid clashes
            var app = (App)System.Windows.Application.Current;

            _login = app.Services.GetRequiredService<LoginService>();
            _users = app.Services.GetRequiredService<IUserRepository>();
            _currentUser = app.Services.GetRequiredService<ICurrentUserService>();

            Loaded += (_, __) => UsernameBox.Focus();
        }

        private async void Login_Click(object sender, RoutedEventArgs e)
        {
            ErrorText.Visibility = Visibility.Collapsed;

            var username = UsernameBox.Text?.Trim() ?? "";
            var password = PasswordBox.Password ?? "";

            if (string.IsNullOrWhiteSpace(username) || string.IsNullOrWhiteSpace(password))
            {
                ShowError("Enter username and password.");
                return;
            }

            var ok = await _login.ValidateAsync(username, password);
            if (ok)
            {
                // Step 5: wire login -> set current user
                var user = await _users.FindByUsernameAsync(username);
                if (user is null)
                {
                    ShowError("User record not found.");
                    return;
                }

                _currentUser.SignIn(
                    userId: user.Id,
                    username: user.Username,
                    displayName: user.DisplayName,
                    roles: user.Roles
                );

                DialogResult = true; // success closes dialog
            }
            else
            {
                ShowError("Invalid credentials. Try again.");
                PasswordBox.Clear();
                PasswordBox.Focus();
            }
        }

        private void Cancel_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false; // cancel/close
        }

        private void ShowError(string message)
        {
            ErrorText.Text = message;
            ErrorText.Visibility = Visibility.Visible;
        }
    }
}
