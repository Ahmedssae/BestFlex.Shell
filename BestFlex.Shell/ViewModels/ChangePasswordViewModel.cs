using System;
using System.ComponentModel;

namespace BestFlex.Shell.ViewModels
{
    public class ChangePasswordViewModel : INotifyPropertyChanged
    {
        private string _currentPassword = string.Empty;
        private string _newPassword = string.Empty;
        private string _confirmPassword = string.Empty;

        public string CurrentPassword
        {
            get => _currentPassword;
            set => SetProperty(ref _currentPassword, value, nameof(CurrentPassword));
        }

        public string NewPassword
        {
            get => _newPassword;
            set => SetProperty(ref _newPassword, value, nameof(NewPassword));
        }

        public string ConfirmPassword
        {
            get => _confirmPassword;
            set => SetProperty(ref _confirmPassword, value, nameof(ConfirmPassword));
        }

        public ChangePasswordViewModel()
        {
        }

        public ChangePasswordViewModel(string userId, string currentPassword)
        {
            CurrentPassword = currentPassword;
        }

        public event PropertyChangedEventHandler? PropertyChanged;

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
