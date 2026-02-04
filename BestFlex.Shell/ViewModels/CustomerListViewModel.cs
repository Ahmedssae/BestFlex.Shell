using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using System.Windows.Input;
using BestFlex.Application.UI;
using BestFlex.Shell.Infrastructure;

namespace BestFlex.Shell.ViewModels
{
    public class CustomerListViewModel : INotifyPropertyChanged
    {
        private readonly ICustomerUiAdapter _customerUiAdapter;
        private ObservableCollection<CustomerUiModel> _customers;
        private CustomerUiModel? _selectedCustomer;
        private bool _isLoading;
        private string _searchTerm = string.Empty;
        private string _errorMessage = string.Empty;
        private bool _includeInactive;

        public CustomerListViewModel(ICustomerUiAdapter customerUiAdapter)
        {
            _customerUiAdapter = customerUiAdapter ?? throw new ArgumentNullException(nameof(customerUiAdapter));
            _customers = new ObservableCollection<CustomerUiModel>();
            
            LoadCustomersCommand = new AsyncRelayCommand(LoadCustomersAsync);
            RefreshCommand = new AsyncRelayCommand(RefreshCustomersAsync);
            AddCustomerCommand = new AsyncRelayCommand(AddCustomerAsync);
            EditCustomerCommand = new AsyncRelayCommand(EditCustomerAsync, () => SelectedCustomer != null);
            DeactivateCustomerCommand = new AsyncRelayCommand(DeactivateCustomerAsync, () => SelectedCustomer != null && SelectedCustomer.IsActive);
        }

        public ObservableCollection<CustomerUiModel> Customers
        {
            get => _customers;
            set => SetProperty(ref _customers, value);
        }

        public CustomerUiModel? SelectedCustomer
        {
            get => _selectedCustomer;
            set
            {
                if (SetProperty(ref _selectedCustomer, value))
                {
                    ((AsyncRelayCommand)EditCustomerCommand).RaiseCanExecuteChanged();
                    ((AsyncRelayCommand)DeactivateCustomerCommand).RaiseCanExecuteChanged();
                }
            }
        }

        public bool IsLoading
        {
            get => _isLoading;
            set => SetProperty(ref _isLoading, value);
        }

        public string SearchTerm
        {
            get => _searchTerm;
            set
            {
                if (SetProperty(ref _searchTerm, value))
                {
                    _ = LoadCustomersAsync(); // Debounce would be better in real app
                }
            }
        }

        public bool IncludeInactive
        {
            get => _includeInactive;
            set
            {
                if (SetProperty(ref _includeInactive, value))
                {
                    _ = LoadCustomersAsync();
                }
            }
        }

        public string ErrorMessage
        {
            get => _errorMessage;
            set => SetProperty(ref _errorMessage, value);
        }

        public ICommand LoadCustomersCommand { get; }
        public ICommand RefreshCommand { get; }
        public ICommand AddCustomerCommand { get; }
        public ICommand EditCustomerCommand { get; }
        public ICommand DeactivateCustomerCommand { get; }

        private async Task LoadCustomersAsync()
        {
            try
            {
                IsLoading = true;
                ErrorMessage = string.Empty;

                // In Phase 7B, this would call a real customer list use case
                // For now, simulate loading customers from database
                await Task.Delay(500); // Simulate network delay

                // In real implementation, this would call the adapter to get customers
                var mockCustomers = new[]
                {
                    new CustomerUiModel { Id = 1, Name = "Acme Corporation", TaxId = "123456789", CreditLimit = 10000, PaymentTermsDays = 30, IsActive = true },
                    new CustomerUiModel { Id = 2, Name = "Global Industries", TaxId = "987654321", CreditLimit = 25000, PaymentTermsDays = 45, IsActive = true },
                    new CustomerUiModel { Id = 3, Name = "Local Business LLC", TaxId = "456789123", CreditLimit = 5000, PaymentTermsDays = 15, IsActive = false }
                };

                Customers.Clear();
                foreach (var customer in mockCustomers)
                {
                    if (!string.IsNullOrWhiteSpace(SearchTerm) && !customer.Name.Contains(SearchTerm, StringComparison.OrdinalIgnoreCase))
                        continue;
                        
                    if (!IncludeInactive && !customer.IsActive)
                        continue;
                        
                    Customers.Add(customer);
                }
            }
            catch (Exception ex)
            {
                ErrorMessage = "Failed to load customers: " + ex.Message;
            }
            finally
            {
                IsLoading = false;
            }
        }

        private async Task RefreshCustomersAsync()
        {
            await LoadCustomersAsync();
        }

        private async Task AddCustomerAsync()
        {
            // In Phase 7B, this would open the Add Customer dialog
            // For now, just show a message
            await Task.CompletedTask;
            ErrorMessage = "Add Customer feature coming soon";
        }

        private async Task EditCustomerAsync()
        {
            if (SelectedCustomer == null) return;

            // In Phase 7B, this would open the Edit Customer dialog
            // For now, just show a message
            await Task.CompletedTask;
            ErrorMessage = $"Edit Customer '{SelectedCustomer.Name}' feature coming soon";
        }

        private async Task DeactivateCustomerAsync()
        {
            if (SelectedCustomer == null) return;

            try
            {
                IsLoading = true;
                ErrorMessage = string.Empty;

                var request = new DeactivateCustomerUiRequest
                {
                    CustomerId = SelectedCustomer.Id,
                    Reason = "Deactivated by user"
                };

                var result = await _customerUiAdapter.DeactivateCustomerAsync(request);

                if (result.Success)
                {
                    await LoadCustomersAsync(); // Refresh the list
                }
                else
                {
                    ErrorMessage = result.UserFriendlyMessage;
                }
            }
            catch (Exception ex)
            {
                ErrorMessage = "Failed to deactivate customer: " + ex.Message;
            }
            finally
            {
                IsLoading = false;
            }
        }

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

    // UI Model for Customer
    public class CustomerUiModel
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string TaxId { get; set; } = string.Empty;
        public decimal CreditLimit { get; set; }
        public int PaymentTermsDays { get; set; }
        public bool IsActive { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime? UpdatedAt { get; set; }
    }
}
