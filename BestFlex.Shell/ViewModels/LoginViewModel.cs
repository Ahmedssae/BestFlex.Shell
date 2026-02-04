using System;
using System.ComponentModel;
using System.Diagnostics;
using System.Windows.Input;

namespace BestFlex.Shell.ViewModels
{
    public class LoginViewModel : INotifyPropertyChanged
    {
        private string _username = string.Empty;
        private string _password = string.Empty;
        private bool _isBusy = false;
        private string _errorMessage = string.Empty;

        public ICommand LoginCommand { get; }

        public string Username
        {
            get => _username;
            set 
            { 
                if (SetProperty(ref _username, value, nameof(Username)))
                {
                    ((AsyncRelayCommand)LoginCommand).RaiseCanExecuteChanged();
                }
            }
        }

        public string Password
        {
            get => _password;
            set 
            { 
                if (SetProperty(ref _password, value, nameof(Password)))
                {
                    ((AsyncRelayCommand)LoginCommand).RaiseCanExecuteChanged();
                }
            }
        }

        public bool IsBusy
        {
            get => _isBusy;
            set 
            { 
                if (SetProperty(ref _isBusy, value, nameof(IsBusy)))
                {
                    ((AsyncRelayCommand)LoginCommand).RaiseCanExecuteChanged();
                }
            }
        }

        public string ErrorMessage
        {
            get => _errorMessage;
            set => SetProperty(ref _errorMessage, value, nameof(ErrorMessage));
        }

        public event Action? LoginSucceeded;

        public event PropertyChangedEventHandler? PropertyChanged;

        public LoginViewModel()
        {
            LoginCommand = new AsyncRelayCommand(LoginAsync, CanLogin);
        }

        private bool CanLogin() =>
            !IsBusy &&
            !string.IsNullOrWhiteSpace(Username) &&
            !string.IsNullOrWhiteSpace(Password);

        private async Task LoginAsync()
        {
            try
            {
                IsBusy = true;
                ErrorMessage = string.Empty;

                // Simulate login validation (replace with real authentication)
                await Task.Delay(1000); // Simulate network call

                // Simple validation for demo purposes
                if (Username == "admin" && Password == "admin")
                {
                    LoginSucceeded?.Invoke();
                }
                else if (Username == "test" && Password == "test")
                {
                    LoginSucceeded?.Invoke();
                }
                else
                {
                    ErrorMessage = "Invalid username or password";
                }
            }
            catch (Exception ex)
            {
                ErrorMessage = "Login failed. Please try again.";
                // Log the exception for debugging (could be sent to logging service)
                System.Diagnostics.Debug.WriteLine($"Login error: {ex.Message}");
            }
            finally
            {
                IsBusy = false;
            }
        }

        protected virtual void OnPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        protected bool SetProperty<T>(ref T field, T value, string propertyName)
        {
            if (Equals(field, value)) return false;
            field = value;
            OnPropertyChanged(propertyName);
            return true;
        }
    }
}
