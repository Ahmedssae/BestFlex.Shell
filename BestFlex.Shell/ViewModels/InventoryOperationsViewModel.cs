using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Input;
using BestFlex.Application.Abstractions;
using BestFlex.Application.UI;
using BestFlex.Shell.Abstractions;

namespace BestFlex.Shell.ViewModels
{
    public class ReceiveStockViewModel : INotifyPropertyChanged
    {
        private readonly IInventoryUiAdapter _inventoryAdapter;
        private readonly ICurrentUserService _currentUser;
        
        private int _selectedProductId;
        private string _selectedProductName = string.Empty;
        private decimal _quantity;
        private decimal _unitCost;
        private string _referenceNumber = string.Empty;
        private string _notes = string.Empty;
        private bool _isLoading = false;
        private string _errorMessage = string.Empty;
        private ObservableCollection<InventoryValidationError> _validationErrors = new();
        private ObservableCollection<BestFlex.Application.UI.ProductLookupDto> _availableProducts = new();
        private ICommand? _receiveStockCommand;
        private ICommand? _cancelCommand;
        private decimal _newStockLevel;

        public ReceiveStockViewModel(IInventoryUiAdapter inventoryAdapter, ICurrentUserService currentUser)
        {
            _inventoryAdapter = inventoryAdapter;
            _currentUser = currentUser;
            InitializeCommands();
            _ = LoadAvailableProductsAsync(); // Fire and forget for constructor
        }

        public int SelectedProductId
        {
            get => _selectedProductId;
            set => SetProperty(ref _selectedProductId, value, nameof(SelectedProductId));
        }

        public string SelectedProductName
        {
            get => _selectedProductName;
            set => SetProperty(ref _selectedProductName, value, nameof(SelectedProductName));
        }

        public decimal Quantity
        {
            get => _quantity;
            set => SetProperty(ref _quantity, value, nameof(Quantity));
        }

        public decimal UnitCost
        {
            get => _unitCost;
            set => SetProperty(ref _unitCost, value, nameof(UnitCost));
        }

        public string ReferenceNumber
        {
            get => _referenceNumber;
            set => SetProperty(ref _referenceNumber, value, nameof(ReferenceNumber));
        }

        public string Notes
        {
            get => _notes;
            set => SetProperty(ref _notes, value, nameof(Notes));
        }

        public bool IsLoading
        {
            get => _isLoading;
            set => SetProperty(ref _isLoading, value, nameof(IsLoading));
        }

        public string ErrorMessage
        {
            get => _errorMessage;
            set => SetProperty(ref _errorMessage, value, nameof(ErrorMessage));
        }

        public ObservableCollection<InventoryValidationError> ValidationErrors
        {
            get => _validationErrors;
            set => SetProperty(ref _validationErrors, value, nameof(ValidationErrors));
        }

        public ObservableCollection<BestFlex.Application.UI.ProductLookupDto> AvailableProducts
        {
            get => _availableProducts;
            set => SetProperty(ref _availableProducts, value, nameof(AvailableProducts));
        }

        public decimal NewStockLevel
        {
            get => _newStockLevel;
            set => SetProperty(ref _newStockLevel, value, nameof(NewStockLevel));
        }

        public ICommand ReceiveStockCommand => _receiveStockCommand ??= new BestFlex.Shell.Infrastructure.RelayCommand(async () => await ReceiveStockAsync());
        public ICommand CancelCommand => _cancelCommand ??= new BestFlex.Shell.Infrastructure.RelayCommand(Cancel);

        private void InitializeCommands()
        {
            _receiveStockCommand = new BestFlex.Shell.Infrastructure.RelayCommand(async () => await ReceiveStockAsync());
            _cancelCommand = new BestFlex.Shell.Infrastructure.RelayCommand(Cancel);
        }

        public async Task LoadAvailableProductsAsync()
        {
            try
            {
                IsLoading = true;
                ErrorMessage = string.Empty;

                // TODO: Load available products from database
                // For now, use mock data to demonstrate UI functionality
                var products = new List<BestFlex.Application.UI.ProductLookupDto>
                {
                    new BestFlex.Application.UI.ProductLookupDto { Id = 1, SKU = "PROD-001", Name = "Sample Product 1", AvailableStock = 1000, BasePrice = 75.00m },
                    new BestFlex.Application.UI.ProductLookupDto { Id = 2, SKU = "PROD-002", Name = "Sample Product 2", AvailableStock = 500, BasePrice = 40.00m },
                    new BestFlex.Application.UI.ProductLookupDto { Id = 3, SKU = "PROD-003", Name = "Sample Product 3", AvailableStock = 200, BasePrice = 45.00m },
                    new BestFlex.Application.UI.ProductLookupDto { Id = 4, SKU = "PROD-004", Name = "High Demand Product", AvailableStock = 2000, BasePrice = 85.00m }
                };

                AvailableProducts.Clear();
                foreach (var product in products)
                {
                    AvailableProducts.Add(product);
                }
            }
            catch (Exception ex)
            {
                ErrorMessage = "Failed to load products: " + ex.Message;
            }
            finally
            {
                IsLoading = false;
            }
        }

        public async Task ReceiveStockAsync()
        {
            try
            {
                IsLoading = true;
                ErrorMessage = string.Empty;
                ValidationErrors.Clear();

                // Validate request
                var validationErrors = ValidateReceiveStockRequest();
                if (validationErrors.Any())
                {
                    ValidationErrors = new ObservableCollection<InventoryValidationError>(validationErrors);
                    return;
                }

                // Create receive stock request
                var request = new ReceiveStockUiRequest
                {
                    ProductId = SelectedProductId,
                    Quantity = Quantity,
                    UnitCost = UnitCost,
                    ReferenceNumber = string.IsNullOrWhiteSpace(ReferenceNumber) ? $"REC-{DateTime.Now:yyyyMMddHHmmss}" : ReferenceNumber,
                    Notes = Notes
                };

                // Call adapter
                var result = await _inventoryAdapter.ReceiveStockAsync(request);
                
                if (result.Success)
                {
                    NewStockLevel = result.NewStockLevel;
                    
                    // Show success message
                    System.Windows.MessageBox.Show(
                        $"Stock received successfully!\n\nNew stock level: {result.NewStockLevel}",
                        "Success",
                        System.Windows.MessageBoxButton.OK,
                        System.Windows.MessageBoxImage.Information);
                    
                    // Close window on success
                    System.Windows.Application.Current.Windows.OfType<System.Windows.Window>()
                        .FirstOrDefault(w => w.DataContext == this)?.Close();
                }
                else
                {
                    ErrorMessage = result.UserFriendlyMessage;
                    ValidationErrors = new ObservableCollection<InventoryValidationError>(result.ValidationErrors);
                }
            }
            catch (Exception ex)
            {
                ErrorMessage = "Failed to receive stock: " + ex.Message;
            }
            finally
            {
                IsLoading = false;
            }
        }

        private List<InventoryValidationError> ValidateReceiveStockRequest()
        {
            var errors = new List<InventoryValidationError>();

            if (SelectedProductId <= 0)
                errors.Add(new InventoryValidationError { PropertyName = "Product", ErrorMessage = "Please select a product" });

            if (Quantity <= 0)
                errors.Add(new InventoryValidationError { PropertyName = "Quantity", ErrorMessage = "Quantity must be greater than 0" });

            if (UnitCost < 0)
                errors.Add(new InventoryValidationError { PropertyName = "UnitCost", ErrorMessage = "Unit cost cannot be negative" });

            if (string.IsNullOrWhiteSpace(Notes))
                errors.Add(new InventoryValidationError { PropertyName = "Notes", ErrorMessage = "Notes are required for audit trail" });

            return errors;
        }

        private void Cancel()
        {
            System.Windows.Application.Current.Windows.OfType<System.Windows.Window>()
                .FirstOrDefault(w => w.DataContext == this)?.Close();
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

    public class AdjustStockViewModel : INotifyPropertyChanged
    {
        private readonly IInventoryUiAdapter _inventoryAdapter;
        private readonly ICurrentUserService _currentUser;
        
        private int _selectedProductId;
        private string _selectedProductName = string.Empty;
        private decimal _quantity;
        private string _movementType = "ADJUST";
        private string _reason = string.Empty;
        private int _managerId;
        private string _referenceNumber = string.Empty;
        private bool _isLoading = false;
        private string _errorMessage = string.Empty;
        private ObservableCollection<InventoryValidationError> _validationErrors = new();
        private ObservableCollection<BestFlex.Application.UI.ProductLookupDto> _availableProducts = new();
        private ICommand? _adjustStockCommand;
        private ICommand? _cancelCommand;
        private decimal _newStockLevel;

        public AdjustStockViewModel(IInventoryUiAdapter inventoryAdapter, ICurrentUserService currentUser)
        {
            _inventoryAdapter = inventoryAdapter;
            _currentUser = currentUser;
            InitializeCommands();
            _managerId = (int)_currentUser.UserId.GetHashCode(); // Convert Guid to int
            _ = LoadAvailableProductsAsync(); // Fire and forget for constructor
        }

        public int SelectedProductId
        {
            get => _selectedProductId;
            set => SetProperty(ref _selectedProductId, value, nameof(SelectedProductId));
        }

        public string SelectedProductName
        {
            get => _selectedProductName;
            set => SetProperty(ref _selectedProductName, value, nameof(SelectedProductName));
        }

        public decimal Quantity
        {
            get => _quantity;
            set => SetProperty(ref _quantity, value, nameof(Quantity));
        }

        public string MovementType
        {
            get => _movementType;
            set => SetProperty(ref _movementType, value, nameof(MovementType));
        }

        public string Reason
        {
            get => _reason;
            set => SetProperty(ref _reason, value, nameof(Reason));
        }

        public int ManagerId
        {
            get => _managerId;
            set => SetProperty(ref _managerId, value, nameof(ManagerId));
        }

        public string ReferenceNumber
        {
            get => _referenceNumber;
            set => SetProperty(ref _referenceNumber, value, nameof(ReferenceNumber));
        }

        public bool IsLoading
        {
            get => _isLoading;
            set => SetProperty(ref _isLoading, value, nameof(IsLoading));
        }

        public string ErrorMessage
        {
            get => _errorMessage;
            set => SetProperty(ref _errorMessage, value, nameof(ErrorMessage));
        }

        public ObservableCollection<InventoryValidationError> ValidationErrors
        {
            get => _validationErrors;
            set => SetProperty(ref _validationErrors, value, nameof(ValidationErrors));
        }

        public ObservableCollection<BestFlex.Application.UI.ProductLookupDto> AvailableProducts
        {
            get => _availableProducts;
            set => SetProperty(ref _availableProducts, value, nameof(AvailableProducts));
        }

        public decimal NewStockLevel
        {
            get => _newStockLevel;
            set => SetProperty(ref _newStockLevel, value, nameof(NewStockLevel));
        }

        public ICommand AdjustStockCommand => _adjustStockCommand ??= new BestFlex.Shell.Infrastructure.RelayCommand(async () => await AdjustStockAsync());
        public ICommand CancelCommand => _cancelCommand ??= new BestFlex.Shell.Infrastructure.RelayCommand(Cancel);

        private void InitializeCommands()
        {
            _adjustStockCommand = new BestFlex.Shell.Infrastructure.RelayCommand(async () => await AdjustStockAsync());
            _cancelCommand = new BestFlex.Shell.Infrastructure.RelayCommand(Cancel);
        }

        public async Task LoadAvailableProductsAsync()
        {
            try
            {
                IsLoading = true;
                ErrorMessage = string.Empty;

                // TODO: Load available products from database
                // For now, use mock data to demonstrate UI functionality
                var products = new List<BestFlex.Application.UI.ProductLookupDto>
                {
                    new BestFlex.Application.UI.ProductLookupDto { Id = 1, SKU = "PROD-001", Name = "Sample Product 1", AvailableStock = 1000, BasePrice = 75.00m },
                    new BestFlex.Application.UI.ProductLookupDto { Id = 2, SKU = "PROD-002", Name = "Sample Product 2", AvailableStock = 500, BasePrice = 40.00m },
                    new BestFlex.Application.UI.ProductLookupDto { Id = 3, SKU = "PROD-003", Name = "Sample Product 3", AvailableStock = 200, BasePrice = 45.00m },
                    new BestFlex.Application.UI.ProductLookupDto { Id = 4, SKU = "PROD-004", Name = "High Demand Product", AvailableStock = 2000, BasePrice = 85.00m }
                };

                AvailableProducts.Clear();
                foreach (var product in products)
                {
                    AvailableProducts.Add(product);
                }
            }
            catch (Exception ex)
            {
                ErrorMessage = "Failed to load products: " + ex.Message;
            }
            finally
            {
                IsLoading = false;
            }
        }

        public async Task AdjustStockAsync()
        {
            try
            {
                IsLoading = true;
                ErrorMessage = string.Empty;
                ValidationErrors.Clear();

                // Validate request
                var validationErrors = ValidateAdjustStockRequest();
                if (validationErrors.Any())
                {
                    ValidationErrors = new ObservableCollection<InventoryValidationError>(validationErrors);
                    return;
                }

                // Create adjust stock request
                var request = new AdjustStockUiRequest
                {
                    ProductId = SelectedProductId,
                    Quantity = Quantity,
                    MovementType = MovementType,
                    Reason = Reason,
                    ManagerId = ManagerId,
                    ReferenceNumber = string.IsNullOrWhiteSpace(ReferenceNumber) ? $"ADJ-{DateTime.Now:yyyyMMddHHmmss}" : ReferenceNumber
                };

                // Call adapter
                var result = await _inventoryAdapter.AdjustStockAsync(request);
                
                if (result.Success)
                {
                    NewStockLevel = result.NewStockLevel;
                    
                    // Show success message
                    System.Windows.MessageBox.Show(
                        $"Stock adjusted successfully!\n\nNew stock level: {result.NewStockLevel}",
                        "Success",
                        System.Windows.MessageBoxButton.OK,
                        System.Windows.MessageBoxImage.Information);
                    
                    // Close window on success
                    System.Windows.Application.Current.Windows.OfType<System.Windows.Window>()
                        .FirstOrDefault(w => w.DataContext == this)?.Close();
                }
                else
                {
                    ErrorMessage = result.UserFriendlyMessage;
                    ValidationErrors = new ObservableCollection<InventoryValidationError>(result.ValidationErrors);
                }
            }
            catch (Exception ex)
            {
                ErrorMessage = "Failed to adjust stock: " + ex.Message;
            }
            finally
            {
                IsLoading = false;
            }
        }

        private List<InventoryValidationError> ValidateAdjustStockRequest()
        {
            var errors = new List<InventoryValidationError>();

            if (SelectedProductId <= 0)
                errors.Add(new InventoryValidationError { PropertyName = "Product", ErrorMessage = "Please select a product" });

            if (Quantity == 0)
                errors.Add(new InventoryValidationError { PropertyName = "Quantity", ErrorMessage = "Quantity cannot be zero" });

            if (string.IsNullOrWhiteSpace(Reason))
                errors.Add(new InventoryValidationError { PropertyName = "Reason", ErrorMessage = "Reason is required for audit trail" });

            if (ManagerId <= 0)
                errors.Add(new InventoryValidationError { PropertyName = "ManagerId", ErrorMessage = "Valid manager ID is required" });

            return errors;
        }

        private void Cancel()
        {
            System.Windows.Application.Current.Windows.OfType<System.Windows.Window>()
                .FirstOrDefault(w => w.DataContext == this)?.Close();
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
