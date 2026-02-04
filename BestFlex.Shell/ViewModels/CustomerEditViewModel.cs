using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using System.Windows.Input;
using BestFlex.Application.UI;
using BestFlex.Shell.Infrastructure;

namespace BestFlex.Shell.ViewModels
{
    public class CustomerEditViewModel : INotifyPropertyChanged
    {
        private readonly ICustomerUiAdapter _customerUiAdapter;
        private readonly int? _customerId;
        private bool _isLoading;
        private string _errorMessage = string.Empty;
        private string _name = string.Empty;
        private string _taxId = string.Empty;
        private decimal _creditLimit;
        private int _paymentTermsDays = 30;
        private bool _isActive = true;
        private ObservableCollection<CustomerValidationError> _validationErrors = new();

        public CustomerEditViewModel(ICustomerUiAdapter customerUiAdapter, int? customerId = null)
        {
            _customerUiAdapter = customerUiAdapter ?? throw new ArgumentNullException(nameof(customerUiAdapter));
            _customerId = customerId;
            
            SaveCommand = new AsyncRelayCommand(SaveAsync, CanSave);
            CancelCommand = new AsyncRelayCommand(CancelAsync);
            ChangeCreditLimitCommand = new AsyncRelayCommand(ChangeCreditLimitAsync, CanChangeCreditLimit);

            if (_customerId.HasValue)
            {
                Title = "Edit Customer";
                _ = LoadCustomerAsync();
            }
            else
            {
                Title = "Add Customer";
            }
        }

        public string Title { get; private set; } = string.Empty;

        public string Name
        {
            get => _name;
            set
            {
                if (SetProperty(ref _name, value))
                {
                    ValidateProperty(nameof(Name));
                    ((AsyncRelayCommand)SaveCommand).RaiseCanExecuteChanged();
                }
            }
        }

        public string TaxId
        {
            get => _taxId;
            set
            {
                if (SetProperty(ref _taxId, value))
                {
                    ValidateProperty(nameof(TaxId));
                    ((AsyncRelayCommand)SaveCommand).RaiseCanExecuteChanged();
                }
            }
        }

        public decimal CreditLimit
        {
            get => _creditLimit;
            set
            {
                if (SetProperty(ref _creditLimit, value))
                {
                    ValidateProperty(nameof(CreditLimit));
                    ((AsyncRelayCommand)SaveCommand).RaiseCanExecuteChanged();
                    ((AsyncRelayCommand)ChangeCreditLimitCommand).RaiseCanExecuteChanged();
                }
            }
        }

        public int PaymentTermsDays
        {
            get => _paymentTermsDays;
            set
            {
                if (SetProperty(ref _paymentTermsDays, value))
                {
                    ValidateProperty(nameof(PaymentTermsDays));
                    ((AsyncRelayCommand)SaveCommand).RaiseCanExecuteChanged();
                }
            }
        }

        public bool IsActive
        {
            get => _isActive;
            set => SetProperty(ref _isActive, value);
        }

        public bool IsLoading
        {
            get => _isLoading;
            set => SetProperty(ref _isLoading, value);
        }

        public string ErrorMessage
        {
            get => _errorMessage;
            set => SetProperty(ref _errorMessage, value);
        }

        public ObservableCollection<CustomerValidationError> ValidationErrors
        {
            get => _validationErrors;
            set => SetProperty(ref _validationErrors, value);
        }

        public ICommand SaveCommand { get; }
        public ICommand CancelCommand { get; }
        public ICommand ChangeCreditLimitCommand { get; }

        private async Task LoadCustomerAsync()
        {
            if (!_customerId.HasValue) return;

            try
            {
                IsLoading = true;
                ErrorMessage = string.Empty;

                // In Phase 7B, this would load customer from database via adapter
                // For now, simulate loading
                await Task.Delay(300);

                // Mock data - in real implementation, this would come from adapter
                Name = "Acme Corporation";
                TaxId = "123456789";
                CreditLimit = 10000;
                PaymentTermsDays = 30;
                IsActive = true;
            }
            catch (Exception ex)
            {
                ErrorMessage = "Failed to load customer: " + ex.Message;
            }
            finally
            {
                IsLoading = false;
            }
        }

        private async Task SaveAsync()
        {
            try
            {
                IsLoading = true;
                ErrorMessage = string.Empty;
                ValidationErrors.Clear();

                if (_customerId.HasValue)
                {
                    // Update existing customer
                    var request = new UpdateCustomerUiRequest
                    {
                        Id = _customerId.Value,
                        Name = Name,
                        CreditLimit = CreditLimit,
                        PaymentTermsDays = PaymentTermsDays
                    };

                    var result = await _customerUiAdapter.UpdateCustomerAsync(request);

                    if (result.Success)
                    {
                        // Close dialog with success
                        CloseDialog?.Invoke(true);
                    }
                    else
                    {
                        ErrorMessage = result.UserFriendlyMessage;
                        ValidationErrors.Clear();
                        foreach (var error in result.ValidationErrors)
                        {
                            ValidationErrors.Add(error);
                        }
                    }
                }
                else
                {
                    // Create new customer
                    var request = new CreateCustomerUiRequest
                    {
                        Name = Name,
                        TaxId = TaxId,
                        CreditLimit = CreditLimit,
                        PaymentTermsDays = PaymentTermsDays
                    };

                    var result = await _customerUiAdapter.CreateCustomerAsync(request);

                    if (result.Success)
                    {
                        // Close dialog with success
                        CloseDialog?.Invoke(true);
                    }
                    else
                    {
                        ErrorMessage = result.UserFriendlyMessage;
                        ValidationErrors.Clear();
                        foreach (var error in result.ValidationErrors)
                        {
                            ValidationErrors.Add(error);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                ErrorMessage = "Failed to save customer: " + ex.Message;
            }
            finally
            {
                IsLoading = false;
            }
        }

        private async Task CancelAsync()
        {
            await Task.CompletedTask;
            CloseDialog?.Invoke(false);
        }

        private async Task ChangeCreditLimitAsync()
        {
            if (!_customerId.HasValue) return;

            try
            {
                IsLoading = true;
                ErrorMessage = string.Empty;

                var request = new ChangeCreditLimitUiRequest
                {
                    CustomerId = _customerId.Value,
                    NewCreditLimit = CreditLimit,
                    Reason = "Updated by user"
                };

                var result = await _customerUiAdapter.ChangeCreditLimitAsync(request);

                if (result.Success)
                {
                    ErrorMessage = "Credit limit updated successfully";
                }
                else
                {
                    ErrorMessage = result.UserFriendlyMessage;
                    ValidationErrors.Clear();
                    foreach (var error in result.ValidationErrors)
                    {
                        ValidationErrors.Add(error);
                    }
                }
            }
            catch (Exception ex)
            {
                ErrorMessage = "Failed to change credit limit: " + ex.Message;
            }
            finally
            {
                IsLoading = false;
            }
        }

        private bool CanSave()
        {
            return !string.IsNullOrWhiteSpace(Name) &&
                   !string.IsNullOrWhiteSpace(TaxId) &&
                   CreditLimit >= 0 &&
                   PaymentTermsDays >= 0 &&
                   !IsLoading &&
                   ValidationErrors.Count == 0;
        }

        private bool CanChangeCreditLimit()
        {
            return _customerId.HasValue && CreditLimit >= 0 && !IsLoading;
        }

        private void ValidateProperty(string propertyName)
        {
            var errors = new List<CustomerValidationError>();

            switch (propertyName)
            {
                case nameof(Name):
                    if (string.IsNullOrWhiteSpace(Name))
                        errors.Add(new CustomerValidationError { PropertyName = propertyName, ErrorMessage = "Customer name is required" });
                    else if (Name.Length < 2)
                        errors.Add(new CustomerValidationError { PropertyName = propertyName, ErrorMessage = "Customer name must be at least 2 characters" });
                    break;

                case nameof(TaxId):
                    if (string.IsNullOrWhiteSpace(TaxId))
                        errors.Add(new CustomerValidationError { PropertyName = propertyName, ErrorMessage = "Tax ID is required" });
                    else if (TaxId.Length < 5)
                        errors.Add(new CustomerValidationError { PropertyName = propertyName, ErrorMessage = "Tax ID must be at least 5 characters" });
                    break;

                case nameof(CreditLimit):
                    if (CreditLimit < 0)
                        errors.Add(new CustomerValidationError { PropertyName = propertyName, ErrorMessage = "Credit limit cannot be negative" });
                    break;

                case nameof(PaymentTermsDays):
                    if (PaymentTermsDays < 0)
                        errors.Add(new CustomerValidationError { PropertyName = propertyName, ErrorMessage = "Payment terms days cannot be negative" });
                    break;
            }

            // Remove existing errors for this property
            var existingErrors = ValidationErrors.Where(e => e.PropertyName != propertyName).ToList();
            
            // Add new errors
            existingErrors.AddRange(errors);
            
            ValidationErrors = new ObservableCollection<CustomerValidationError>(existingErrors);
        }

        public Action<bool>? CloseDialog { get; set; }

        public event PropertyChangedEventHandler? PropertyChanged;

        protected virtual void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        protected bool SetProperty<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
        {
            if (Equals(field, value)) return false;
            field = value;
            OnPropertyChanged(propertyName);
            return true;
        }
    }
}
