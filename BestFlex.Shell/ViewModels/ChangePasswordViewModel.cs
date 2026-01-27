using System;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using BestFlex.Application.Abstractions;
using BestFlex.Infrastructure.Services;
using BestFlex.Persistence.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using BCryptNet = BCrypt.Net.BCrypt;

namespace BestFlex.Shell.ViewModels
{
    public sealed class ChangePasswordViewModel : ViewModelBase
    {
        private readonly IServiceProvider _sp;
        private readonly ICurrentUserService _currentUser;
        private readonly IUserRepository _userRepository;
        private readonly IAuditService _audit;
        private readonly IErrorService _error;

        private readonly AsyncRelayCommand _changePasswordCommand;

        public ChangePasswordViewModel(IServiceProvider sp, IAuditService audit)
        {
            _sp = sp ?? throw new ArgumentNullException(nameof(sp));
            _audit = audit ?? throw new ArgumentNullException(nameof(audit));
            _currentUser = sp.GetRequiredService<ICurrentUserService>();
            _userRepository = sp.GetRequiredService<IUserRepository>();
            _error = sp.GetRequiredService<IErrorService>();
            
            _changePasswordCommand = new AsyncRelayCommand(ChangePasswordAsync, () => CanChangePassword);
            ChangePasswordCommand = _changePasswordCommand;
        }

        private string _currentPassword = string.Empty;
        public string CurrentPassword
        {
            get => _currentPassword;
            set
            {
                if (SetProperty(ref _currentPassword, value))
                {
                    OnPropertyChanged(nameof(CanChangePassword));
                    _changePasswordCommand?.RaiseCanExecuteChanged();
                }
            }
        }

        private string _newPassword = string.Empty;
        public string NewPassword
        {
            get => _newPassword;
            set
            {
                if (SetProperty(ref _newPassword, value))
                {
                    OnPropertyChanged(nameof(CanChangePassword));
                    OnPropertyChanged(nameof(ConfirmPasswordMatch));
                    _changePasswordCommand?.RaiseCanExecuteChanged();
                }
            }
        }

        private string _confirmPassword = string.Empty;
        public string ConfirmPassword
        {
            get => _confirmPassword;
            set
            {
                if (SetProperty(ref _confirmPassword, value))
                {
                    OnPropertyChanged(nameof(CanChangePassword));
                    OnPropertyChanged(nameof(ConfirmPasswordMatch));
                    _changePasswordCommand?.RaiseCanExecuteChanged();
                }
            }
        }

        private bool _isBusy;
        public bool IsBusy
        {
            get => _isBusy;
            private set
            {
                if (SetProperty(ref _isBusy, value))
                {
                    OnPropertyChanged(nameof(CanChangePassword));
                    _changePasswordCommand?.RaiseCanExecuteChanged();
                }
            }
        }

        private string _validationMessage = string.Empty;
        public string ValidationMessage
        {
            get => _validationMessage;
            private set => SetProperty(ref _validationMessage, value);
        }

        public bool ConfirmPasswordMatch => string.IsNullOrEmpty(NewPassword) || NewPassword == ConfirmPassword;
        
        public bool CanChangePassword => !IsBusy 
            && !string.IsNullOrWhiteSpace(CurrentPassword)
            && !string.IsNullOrWhiteSpace(NewPassword)
            && !string.IsNullOrWhiteSpace(ConfirmPassword)
            && NewPassword != CurrentPassword
            && ConfirmPasswordMatch
            && NewPassword.Length >= 6; // Basic password length requirement

        public ICommand ChangePasswordCommand { get; }

        private async Task ChangePasswordAsync()
        {
            if (!CanChangePassword) return;

            try
            {
                IsBusy = true;
                ValidationMessage = string.Empty;

                // Validate current password
                var loginService = _sp.GetRequiredService<LoginService>();
                var isCurrentPasswordValid = await loginService.ValidateAsync(_currentUser.Username, CurrentPassword);
                
                if (!isCurrentPasswordValid)
                {
                    ValidationMessage = "Current password is incorrect.";
                    return;
                }

                // Hash new password
                var newPasswordHash = BCryptNet.HashPassword(NewPassword);

                // Update password in database
                using var scope = _sp.CreateScope();
                var db = scope.ServiceProvider.GetRequiredService<BestFlexDbContext>();
                
                var user = await db.Users.FindAsync(_currentUser.UserId);
                if (user == null)
                {
                    ValidationMessage = "User not found in database.";
                    return;
                }

                user.PasswordHash = newPasswordHash;
                await db.SaveChangesAsync();

                // Log successful password change
                await _audit.LogSecurityAsync("PASSWORD_CHANGED", $"User {_currentUser.Username} changed password");

                ValidationMessage = "Password changed successfully!";
                
                // Close dialog after successful change
                await System.Windows.Threading.Dispatcher.CurrentDispatcher.BeginInvoke(new Action(() =>
                {
                    var window = System.Windows.Application.Current?.Windows.OfType<ChangePasswordWindow>().FirstOrDefault();
                    if (window != null)
                    {
                        window.DialogResult = true;
                        window.Close();
                    }
                }));
            }
            catch (Exception ex)
            {
                _error.Handle(ex, "ChangePasswordViewModel.ChangePasswordAsync");
                ValidationMessage = "Failed to change password. Please try again.";
            }
            finally
            {
                IsBusy = false;
            }
        }
    }
}
