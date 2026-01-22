using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using System.Windows.Input;
using BCryptNet = BCrypt.Net.BCrypt;
using BestFlex.Application.Abstractions;
using BestFlex.Domain;
using BestFlex.Infrastructure.Services;
using BestFlex.Shell.Infrastructure;
using Microsoft.Extensions.Logging;

namespace BestFlex.Shell.ViewModels
{
    public class LoginViewModel : INotifyPropertyChanged
    {
        private readonly LoginService _login;
        private readonly IUserRepository _users;
        private readonly ICurrentUserService _currentUser;
        private readonly ILogger<LoginViewModel> _logger;
        private readonly IForensicLogger _forensicLogger;
        
        private string _errorMessage = string.Empty;
        private bool _isBusy;
        private bool _loginSucceeded;
        
        private string _username = string.Empty;
        private string _password = string.Empty;

        public event PropertyChangedEventHandler? PropertyChanged;

        protected void OnPropertyChanged([CallerMemberName] string? name = null)
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));

        public string ErrorMessage
        {
            get => _errorMessage;
            private set
            {
                _errorMessage = value;
                OnPropertyChanged();
            }
        }

        public bool IsBusy
        {
            get => _isBusy;
            private set
            {
                _isBusy = value;
                OnPropertyChanged();
                ((RelayCommand)LoginCommand).RaiseCanExecuteChanged();
            }
        }

        public string Username
        {
            get => _username;
            set
            {
                _username = value;
                OnPropertyChanged();
                ((RelayCommand)LoginCommand).RaiseCanExecuteChanged();
            }
        }

        public string Password
        {
            get => _password;
            set
            {
                _password = value;
                OnPropertyChanged();
                ((RelayCommand)LoginCommand).RaiseCanExecuteChanged();
            }
        }

        // DialogResult removed from VM-driven window control. VM signals success/cancel via dedicated properties.
        public bool LoginSucceeded
        {
            get => _loginSucceeded;
            private set { _loginSucceeded = value; OnPropertyChanged(); }
        }

        public ICommand LoginCommand { get; }

        public LoginViewModel(
            LoginService login,
            IUserRepository users,
            ICurrentUserService currentUser,
            ILogger<LoginViewModel> logger,
            IForensicLogger forensicLogger)
        {
            _login = login ?? throw new ArgumentNullException(nameof(login));
            _users = users ?? throw new ArgumentNullException(nameof(users));
            _currentUser = currentUser ?? throw new ArgumentNullException(nameof(currentUser));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _forensicLogger = forensicLogger ?? throw new ArgumentNullException(nameof(forensicLogger));

            LoginCommand = new RelayCommand(
                ExecuteLogin,
                CanLogin
            );
            // Cancel is UI-handled (window will Close()). ViewModel should not control window lifetime.
        }

        private bool CanLogin()
        {
            return !string.IsNullOrWhiteSpace(Username)
                && !string.IsNullOrWhiteSpace(Password)
                && !IsBusy;
        }

        private async void ExecuteLogin()
        {
            try
            {
                _logger.LogInformation("Login clicked");
                IsBusy = true;
                ErrorMessage = string.Empty;

                _logger.LogDebug("Attempting login for Username='{Username}', Password.Length={Len}", Username, Password?.Length ?? 0);

                if (string.IsNullOrWhiteSpace(Username) || string.IsNullOrWhiteSpace(Password))
                {
                    ErrorMessage = "Enter username and password.";
                    return;
                }

                // First, lookup the user record so we can provide precise failure reasons.
                var user = await _users.FindByUsernameAsync(Username);
                if (user is null)
                {
                    _logger.LogWarning("Login failed: user not found for Username='{Username}'", Username);
                    ErrorMessage = "User not found";
                    
                    // Forensic logging
                    await _forensicLogger.LogAsync(new BestFlex.Domain.ForensicEvent(
                        BestFlex.Domain.ForensicEventType.LoginFailure,
                        DateTime.UtcNow,
                        Environment.MachineName,
                        Username,
                        $"Login failed: user not found for Username='{Username}'",
                        null,
                        null));
                    
                    return;
                }

                // Verify password against stored hash (BCrypt)
                var passwordOk = false;
                try
                {
                    passwordOk = BCryptNet.Verify(Password ?? string.Empty, user.PasswordHash);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Password verification failed for Username='{Username}'", Username);
                }

                _logger.LogDebug("Password verification result for '{Username}': {Result}", Username, passwordOk);

                if (!passwordOk)
                {
                    _logger.LogWarning("Login failed: invalid password for Username='{Username}'", Username);
                    ErrorMessage = "Invalid password";
                    
                    // Forensic logging
                    await _forensicLogger.LogAsync(new BestFlex.Domain.ForensicEvent(
                        BestFlex.Domain.ForensicEventType.LoginFailure,
                        DateTime.UtcNow,
                        Environment.MachineName,
                        Username,
                        $"Login failed: invalid password for Username='{Username}'",
                        null,
                        null));
                    
                    return;
                }

                // Success
                _currentUser.SignIn(
                    userId: user.Id,
                    username: user.Username,
                    displayName: user.DisplayName,
                    roles: user.Roles
                );

                LoginSucceeded = true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Login failed for user: {Username}", Username);
                ErrorMessage = "Login failed. Please try again.";
            }
            finally
            {
                IsBusy = false;
            }
        }
    }
}
