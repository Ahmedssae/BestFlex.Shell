using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Input;
using BestFlex.Application.Abstractions;
using BestFlex.Application.UI;

namespace BestFlex.Shell.ViewModels
{
    public class SalesOrderViewModel : INotifyPropertyChanged
    {
        private readonly ISalesOrderUiAdapter _salesOrderAdapter;
        
        private int _selectedCustomerId;
        private string _selectedCustomerName = string.Empty;
        private decimal _customerCreditLimit;
        private decimal _customerCurrentBalance;
        private ObservableCollection<SalesOrderLineViewModel> _orderLines = new();
        private SalesOrderStatus _status = SalesOrderStatus.Draft;
        private DateTime _orderDate = DateTime.Now;
        private DateTime _deliveryDate = DateTime.Now.AddDays(7);
        private string _notes = string.Empty;
        private bool _isLoading = false;
        private string _errorMessage = string.Empty;
        private ObservableCollection<SalesOrderValidationError> _validationErrors = new();
        private ObservableCollection<BestFlex.Application.UI.CustomerLookupDto> _availableCustomers = new();
        private ObservableCollection<BestFlex.Application.UI.ProductLookupDto> _availableProducts = new();
        private ICommand? _addLineCommand;
        private ICommand? _removeLineCommand;
        private ICommand? _confirmOrderCommand;
        private ICommand? _cancelOrderCommand;
        private ICommand? _refreshCommand;

        // Computed properties
        public decimal Subtotal => OrderLines.Sum(line => line.LineTotal);
        public decimal DiscountAmount => 0; // TODO: Implement discount logic
        public decimal TaxAmount => Subtotal * 0.1m; // TODO: Implement tax logic
        public decimal TotalAmount => Subtotal + TaxAmount - DiscountAmount;
        public decimal AvailableCredit => CustomerCreditLimit - CustomerCurrentBalance;
        public bool CanConfirm => Status == SalesOrderStatus.Draft && OrderLines.Any() && TotalAmount <= AvailableCredit;
        public bool CanCancel => Status != SalesOrderStatus.Cancelled;

        public SalesOrderViewModel(ISalesOrderUiAdapter salesOrderAdapter)
        {
            _salesOrderAdapter = salesOrderAdapter;
            InitializeCommands();
            // Async initialization should be called explicitly by the UI
        }

        public int SelectedCustomerId
        {
            get => _selectedCustomerId;
            set
            {
                if (SetProperty(ref _selectedCustomerId, value, nameof(SelectedCustomerId)))
                {
                    _ = LoadCustomerDetailsAsync(value); // TODO: Make this properly async with cancellation
                }
            }
        }

        public string SelectedCustomerName
        {
            get => _selectedCustomerName;
            set => SetProperty(ref _selectedCustomerName, value, nameof(SelectedCustomerName));
        }

        public decimal CustomerCreditLimit
        {
            get => _customerCreditLimit;
            set => SetProperty(ref _customerCreditLimit, value, nameof(CustomerCreditLimit));
        }

        public decimal CustomerCurrentBalance
        {
            get => _customerCurrentBalance;
            set => SetProperty(ref _customerCurrentBalance, value, nameof(CustomerCurrentBalance));
        }

        public ObservableCollection<SalesOrderLineViewModel> OrderLines
        {
            get => _orderLines;
            set => SetProperty(ref _orderLines, value, nameof(OrderLines));
        }

        public SalesOrderStatus Status
        {
            get => _status;
            set => SetProperty(ref _status, value, nameof(Status));
        }

        public DateTime OrderDate
        {
            get => _orderDate;
            set => SetProperty(ref _orderDate, value, nameof(OrderDate));
        }

        public DateTime DeliveryDate
        {
            get => _deliveryDate;
            set => SetProperty(ref _deliveryDate, value, nameof(DeliveryDate));
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

        public ObservableCollection<SalesOrderValidationError> ValidationErrors
        {
            get => _validationErrors;
            set => SetProperty(ref _validationErrors, value, nameof(ValidationErrors));
        }

        public ObservableCollection<BestFlex.Application.UI.CustomerLookupDto> AvailableCustomers
        {
            get => _availableCustomers;
            set => SetProperty(ref _availableCustomers, value, nameof(AvailableCustomers));
        }

        public ObservableCollection<BestFlex.Application.UI.ProductLookupDto> AvailableProducts
        {
            get => _availableProducts;
            set => SetProperty(ref _availableProducts, value, nameof(AvailableProducts));
        }

        public ICommand AddLineCommand => _addLineCommand ??= new BestFlex.Shell.Infrastructure.RelayCommand(AddOrderLine);
        public ICommand RemoveLineCommand => _removeLineCommand ??= new BestFlex.Shell.Infrastructure.RelayCommand((object? param) => { if (param is SalesOrderLineViewModel line) RemoveOrderLine(line); });
        public ICommand ConfirmOrderCommand => _confirmOrderCommand ??= new BestFlex.Shell.Infrastructure.RelayCommand(async () => await ConfirmOrderAsync(), () => CanConfirm);
        public ICommand CancelOrderCommand => _cancelOrderCommand ??= new BestFlex.Shell.Infrastructure.RelayCommand(async () => await CancelOrderAsync(), () => CanCancel);
        public ICommand RefreshCommand => _refreshCommand ??= new BestFlex.Shell.Infrastructure.RelayCommand(async () => await LoadInitialDataAsync());

        private void InitializeCommands()
        {
            _addLineCommand = new BestFlex.Shell.Infrastructure.RelayCommand(AddOrderLine);
            _removeLineCommand = new BestFlex.Shell.Infrastructure.RelayCommand((object? param) => { if (param is SalesOrderLineViewModel line) RemoveOrderLine(line); });
            _confirmOrderCommand = new BestFlex.Shell.Infrastructure.RelayCommand(async () => await ConfirmOrderAsync(), () => CanConfirm);
            _cancelOrderCommand = new BestFlex.Shell.Infrastructure.RelayCommand(async () => await CancelOrderAsync(), () => CanCancel);
            _refreshCommand = new BestFlex.Shell.Infrastructure.RelayCommand(async () => await LoadInitialDataAsync());
        }

        public async Task LoadInitialDataAsync()
        {
            try
            {
                IsLoading = true;
                ErrorMessage = string.Empty;
                ValidationErrors.Clear();

                // Clear existing data
                AvailableCustomers.Clear();
                AvailableProducts.Clear();

                // TODO: Implement GetCustomersAsync and GetProductsAsync in SalesOrderUiAdapter
                // For now, show loading state and disable actions until data is loaded
                ErrorMessage = "Customer and product data loading not yet implemented. Please contact administrator.";
                
                // Disable actions until data is properly loaded
                // This prevents users from creating orders with fake data
            }
            catch (Exception ex)
            {
                ErrorMessage = "Failed to load initial data: " + ex.Message;
            }
            finally
            {
                IsLoading = false;
            }
        }

        public async Task LoadCustomerDetailsAsync(int customerId)
        {
            try
            {
                if (customerId <= 0)
                {
                    SelectedCustomerName = string.Empty;
                    CustomerCreditLimit = 0;
                    CustomerCurrentBalance = 0;
                    return;
                }

                // TODO: Load customer details from service
                // For now, clear data to show it's not implemented
                SelectedCustomerName = "Customer loading not implemented";
                CustomerCreditLimit = 0;
                CustomerCurrentBalance = 0;
                
                ErrorMessage = "Customer details loading not yet implemented. Please contact administrator.";
            }
            catch (Exception ex)
            {
                ErrorMessage = "Failed to load customer details: " + ex.Message;
            }
        }

        private void AddOrderLine()
        {
            try
            {
                // Prevent adding lines if no products are available
                if (!AvailableProducts.Any())
                {
                    ErrorMessage = "Cannot add order lines: Product data not loaded. Please contact administrator.";
                    return;
                }

                var newLine = new SalesOrderLineViewModel(AvailableProducts.ToList());
                OrderLines.Add(newLine);
            }
            catch (Exception ex)
            {
                ErrorMessage = "Failed to add order line: " + ex.Message;
            }
        }

        private void RemoveOrderLine(SalesOrderLineViewModel line)
        {
            try
            {
                if (line != null)
                {
                    OrderLines.Remove(line);
                }
            }
            catch (Exception ex)
            {
                ErrorMessage = "Failed to remove order line: " + ex.Message;
            }
        }

        public async Task ConfirmOrderAsync()
        {
            try
            {
                IsLoading = true;
                ErrorMessage = string.Empty;
                ValidationErrors.Clear();

                // Prevent confirmation if no customers or products are available
                if (!AvailableCustomers.Any() || !AvailableProducts.Any())
                {
                    ErrorMessage = "Cannot create order: Customer or product data not loaded. Please contact administrator.";
                    return;
                }

                // Validate order
                var validationErrors = ValidateOrder();
                if (validationErrors.Any())
                {
                    ValidationErrors.Clear();
                    foreach (var error in validationErrors)
                        ValidationErrors.Add(error);
                    return;
                }

                // Create sales order request
                var request = new CreateSalesOrderUiRequest
                {
                    CustomerId = SelectedCustomerId,
                    OrderDate = OrderDate,
                    DeliveryDate = DeliveryDate,
                    Notes = Notes,
                    Lines = OrderLines.Select(line => new SalesOrderLineUiRequest
                    {
                        ProductId = line.ProductId,
                        Quantity = line.Quantity,
                        UnitPrice = line.UnitPrice
                    }).ToList()
                };

                // Call adapter
                var result = await _salesOrderAdapter.CreateSalesOrderAsync(request);
                
                if (result.Success)
                {
                    Status = SalesOrderStatus.Confirmed;
                    
                    // Show success message with real order ID
                    System.Windows.MessageBox.Show(
                        $"Sales order created successfully!\n\nOrder ID: {result.OrderId}\nTotal Amount: {TotalAmount:C}",
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
                    ValidationErrors = new ObservableCollection<SalesOrderValidationError>(result.ValidationErrors);
                }
            }
            catch (Exception ex)
            {
                ErrorMessage = "Failed to confirm order: " + ex.Message;
            }
            finally
            {
                IsLoading = false;
            }
        }

        public async Task CancelOrderAsync()
        {
            try
            {
                IsLoading = true;
                ErrorMessage = string.Empty;
                ValidationErrors.Clear();

                if (Status == SalesOrderStatus.Draft)
                {
                    // Just close the window for draft orders
                    Status = SalesOrderStatus.Cancelled;
                    System.Windows.Application.Current.Windows.OfType<System.Windows.Window>()
                        .FirstOrDefault(w => w.DataContext == this)?.Close();
                    return;
                }

                // For confirmed orders, we would need an order ID to cancel
                // TODO: Implement order cancellation for confirmed orders
                ErrorMessage = "Order cancellation for confirmed orders not yet implemented";
            }
            catch (Exception ex)
            {
                ErrorMessage = "Failed to cancel order: " + ex.Message;
            }
            finally
            {
                IsLoading = false;
            }
        }

        private List<SalesOrderValidationError> ValidateOrder()
        {
            var errors = new List<SalesOrderValidationError>();

            if (SelectedCustomerId <= 0)
                errors.Add(new SalesOrderValidationError { PropertyName = "Customer", ErrorMessage = "Please select a customer" });

            if (!OrderLines.Any())
                errors.Add(new SalesOrderValidationError { PropertyName = "Lines", ErrorMessage = "Order must have at least one line item" });

            if (TotalAmount > AvailableCredit)
                errors.Add(new SalesOrderValidationError { PropertyName = "Credit", ErrorMessage = $"Order total ({TotalAmount:C}) exceeds available credit ({AvailableCredit:C})" });

            foreach (var line in OrderLines)
            {
                if (line.ProductId <= 0)
                    errors.Add(new SalesOrderValidationError { PropertyName = "Product", ErrorMessage = "Please select a product for all line items" });

                if (line.Quantity <= 0)
                    errors.Add(new SalesOrderValidationError { PropertyName = "Quantity", ErrorMessage = "Quantity must be greater than 0" });

                if (line.UnitPrice <= 0)
                    errors.Add(new SalesOrderValidationError { PropertyName = "UnitPrice", ErrorMessage = "Unit price must be greater than 0" });

                if (line.Quantity > line.AvailableStock)
                    errors.Add(new SalesOrderValidationError { PropertyName = "Stock", ErrorMessage = $"Insufficient stock for {line.ProductName}. Available: {line.AvailableStock}, Requested: {line.Quantity}" });
            }

            return errors;
        }

        public event PropertyChangedEventHandler? PropertyChanged;

        protected virtual void OnPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
            
            // Notify computed properties when dependencies change
            if (propertyName == nameof(OrderLines) || propertyName == nameof(CustomerCreditLimit) || propertyName == nameof(CustomerCurrentBalance))
            {
                OnPropertyChanged(nameof(Subtotal));
                OnPropertyChanged(nameof(DiscountAmount));
                OnPropertyChanged(nameof(TaxAmount));
                OnPropertyChanged(nameof(TotalAmount));
                OnPropertyChanged(nameof(AvailableCredit));
                OnPropertyChanged(nameof(CanConfirm));
            }
        }

        protected bool SetProperty<T>(ref T field, T value, string propertyName)
        {
            if (Equals(field, value)) return false;
            field = value;
            OnPropertyChanged(propertyName);
            return true;
        }
    }

    public class SalesOrderLineViewModel : INotifyPropertyChanged
    {
        private readonly List<BestFlex.Application.UI.ProductLookupDto> _availableProducts;
        private int _productId;
        private string _productName = string.Empty;
        private decimal _quantity;
        private decimal _unitPrice;
        private decimal _availableStock;

        public SalesOrderLineViewModel(List<BestFlex.Application.UI.ProductLookupDto> availableProducts)
        {
            _availableProducts = availableProducts;
        }

        public int ProductId
        {
            get => _productId;
            set
            {
                if (SetProperty(ref _productId, value, nameof(ProductId)))
                {
                    UpdateProductDetails();
                }
            }
        }

        public string ProductName
        {
            get => _productName;
            set => SetProperty(ref _productName, value, nameof(ProductName));
        }

        public decimal Quantity
        {
            get => _quantity;
            set => SetProperty(ref _quantity, value, nameof(Quantity));
        }

        public decimal UnitPrice
        {
            get => _unitPrice;
            set => SetProperty(ref _unitPrice, value, nameof(UnitPrice));
        }

        public decimal AvailableStock
        {
            get => _availableStock;
            set => SetProperty(ref _availableStock, value, nameof(AvailableStock));
        }

        public decimal LineTotal => Quantity * UnitPrice;

        private void UpdateProductDetails()
        {
            var product = _availableProducts.FirstOrDefault(p => p.Id == ProductId);
            if (product != null)
            {
                ProductName = product.Name;
                UnitPrice = product.BasePrice;
                AvailableStock = product.AvailableStock;
            }
            else
            {
                ProductName = string.Empty;
                UnitPrice = 0;
                AvailableStock = 0;
            }
        }

        public event PropertyChangedEventHandler? PropertyChanged;

        protected virtual void OnPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
            
            // Notify computed properties when dependencies change
            if (propertyName == nameof(Quantity) || propertyName == nameof(UnitPrice))
            {
                OnPropertyChanged(nameof(LineTotal));
            }
        }

        protected bool SetProperty<T>(ref T field, T value, string propertyName)
        {
            if (Equals(field, value)) return false;
            field = value;
            OnPropertyChanged(propertyName);
            return true;
        }
    }

    public enum SalesOrderStatus
    {
        Draft,
        Confirmed,
        Cancelled
    }
}
