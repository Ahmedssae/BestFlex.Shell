using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Input;
using BestFlex.Application.UI;

namespace BestFlex.Shell.ViewModels
{
    public class InvoicePostingViewModel : INotifyPropertyChanged
    {
        private readonly ISalesOrderUiAdapter _salesOrderAdapter;
        private readonly IInvoicePdfExporter _invoicePdfExporter;
        
        private int _selectedOrderId;
        private string _selectedOrderNumber = string.Empty;
        private string _customerName = string.Empty;
        private string _customerAddress = string.Empty;
        private DateTime _invoiceDate = DateTime.Now;
        private DateTime _dueDate = DateTime.Now.AddDays(30);
        private string _invoiceNumber = string.Empty;
        private decimal _subtotal;
        private decimal _taxAmount;
        private decimal _discountAmount;
        private decimal _totalAmount;
        private decimal _paidAmount;
        private decimal _balanceAmount;
        private bool _isLocked = false;
        private bool _isLoading = false;
        private string _errorMessage = string.Empty;
        private ObservableCollection<InvoiceValidationError> _validationErrors = new();
        private ObservableCollection<InvoicePostingLineViewModel> _invoiceLines = new();
        private ObservableCollection<SalesOrderLookupDto> _availableOrders = new();
        private ICommand? _loadOrderCommand;
        private ICommand? _generateInvoiceCommand;
        private ICommand? _postInvoiceCommand;
        private ICommand? _refreshCommand;

        public InvoicePostingViewModel(
            ISalesOrderUiAdapter salesOrderAdapter,
            IInvoicePdfExporter invoicePdfExporter)
        {
            _salesOrderAdapter = salesOrderAdapter;
            _invoicePdfExporter = invoicePdfExporter;
            InitializeCommands();
            _ = LoadAvailableOrdersAsync(); // Fire and forget for constructor
        }

        public int SelectedOrderId
        {
            get => _selectedOrderId;
            set
            {
                if (SetProperty(ref _selectedOrderId, value, nameof(SelectedOrderId)))
                {
                    _ = LoadOrderDetailsAsync(value);
                }
            }
        }

        public string SelectedOrderNumber
        {
            get => _selectedOrderNumber;
            set => SetProperty(ref _selectedOrderNumber, value, nameof(SelectedOrderNumber));
        }

        public string CustomerName
        {
            get => _customerName;
            set => SetProperty(ref _customerName, value, nameof(CustomerName));
        }

        public string CustomerAddress
        {
            get => _customerAddress;
            set => SetProperty(ref _customerAddress, value, nameof(CustomerAddress));
        }

        public DateTime InvoiceDate
        {
            get => _invoiceDate;
            set => SetProperty(ref _invoiceDate, value, nameof(InvoiceDate));
        }

        public DateTime DueDate
        {
            get => _dueDate;
            set => SetProperty(ref _dueDate, value, nameof(DueDate));
        }

        public string InvoiceNumber
        {
            get => _invoiceNumber;
            set => SetProperty(ref _invoiceNumber, value, nameof(InvoiceNumber));
        }

        public decimal Subtotal
        {
            get => _subtotal;
            set => SetProperty(ref _subtotal, value, nameof(Subtotal));
        }

        public decimal TaxAmount
        {
            get => _taxAmount;
            set => SetProperty(ref _taxAmount, value, nameof(TaxAmount));
        }

        public decimal DiscountAmount
        {
            get => _discountAmount;
            set => SetProperty(ref _discountAmount, value, nameof(DiscountAmount));
        }

        public decimal TotalAmount
        {
            get => _totalAmount;
            set => SetProperty(ref _totalAmount, value, nameof(TotalAmount));
        }

        public decimal PaidAmount
        {
            get => _paidAmount;
            set => SetProperty(ref _paidAmount, value, nameof(PaidAmount));
        }

        public decimal BalanceAmount
        {
            get => _balanceAmount;
            set => SetProperty(ref _balanceAmount, value, nameof(BalanceAmount));
        }

        public bool IsLocked
        {
            get => _isLocked;
            set => SetProperty(ref _isLocked, value, nameof(IsLocked));
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

        public ObservableCollection<InvoiceValidationError> ValidationErrors
        {
            get => _validationErrors;
            set => SetProperty(ref _validationErrors, value, nameof(ValidationErrors));
        }

        public ObservableCollection<InvoicePostingLineViewModel> InvoiceLines
        {
            get => _invoiceLines;
            set => SetProperty(ref _invoiceLines, value, nameof(InvoiceLines));
        }

        public ObservableCollection<SalesOrderLookupDto> AvailableOrders
        {
            get => _availableOrders;
            set => SetProperty(ref _availableOrders, value, nameof(AvailableOrders));
        }

        public ICommand LoadOrderCommand => _loadOrderCommand ??= new BestFlex.Shell.Infrastructure.RelayCommand(async () => await LoadOrderDetailsAsync(SelectedOrderId));
        public ICommand GenerateInvoiceCommand => _generateInvoiceCommand ??= new BestFlex.Shell.Infrastructure.RelayCommand(GenerateInvoiceNumber, () => !IsLocked && SelectedOrderId > 0);
        public ICommand PostInvoiceCommand => _postInvoiceCommand ??= new BestFlex.Shell.Infrastructure.RelayCommand(async () => await PostInvoiceAsync(), () => !IsLocked && !string.IsNullOrEmpty(InvoiceNumber));
        public ICommand RefreshCommand => _refreshCommand ??= new BestFlex.Shell.Infrastructure.RelayCommand(async () => await LoadAvailableOrdersAsync());

        private void InitializeCommands()
        {
            _loadOrderCommand = new BestFlex.Shell.Infrastructure.RelayCommand(async () => await LoadOrderDetailsAsync(SelectedOrderId));
            _generateInvoiceCommand = new BestFlex.Shell.Infrastructure.RelayCommand(GenerateInvoiceNumber, () => !IsLocked && SelectedOrderId > 0);
            _postInvoiceCommand = new BestFlex.Shell.Infrastructure.RelayCommand(async () => await PostInvoiceAsync(), () => !IsLocked && !string.IsNullOrEmpty(InvoiceNumber));
            _refreshCommand = new BestFlex.Shell.Infrastructure.RelayCommand(async () => await LoadAvailableOrdersAsync());
        }

        public async Task LoadAvailableOrdersAsync()
        {
            try
            {
                IsLoading = true;
                ErrorMessage = string.Empty;

                // TODO: Load available sales orders from service
                // For now, use mock data to demonstrate UI functionality
                var orders = new List<SalesOrderLookupDto>
                {
                    new SalesOrderLookupDto { Id = 1, OrderNumber = "SO-2024-001", CustomerName = "Customer 1", TotalAmount = 1500.00m, Status = "Confirmed", OrderDate = DateTime.Now.AddDays(-5) },
                    new SalesOrderLookupDto { Id = 2, OrderNumber = "SO-2024-002", CustomerName = "Customer 2", TotalAmount = 2500.00m, Status = "Confirmed", OrderDate = DateTime.Now.AddDays(-3) },
                    new SalesOrderLookupDto { Id = 3, OrderNumber = "SO-2024-003", CustomerName = "Customer 3", TotalAmount = 800.00m, Status = "Confirmed", OrderDate = DateTime.Now.AddDays(-1) }
                };

                AvailableOrders.Clear();
                foreach (var order in orders)
                {
                    AvailableOrders.Add(order);
                }
            }
            catch (Exception ex)
            {
                ErrorMessage = "Failed to load available orders: " + ex.Message;
            }
            finally
            {
                IsLoading = false;
            }
        }

        public async Task LoadOrderDetailsAsync(int orderId)
        {
            try
            {
                if (orderId <= 0)
                {
                    ClearOrderDetails();
                    return;
                }

                IsLoading = true;
                ErrorMessage = string.Empty;

                // TODO: Load order details from service
                // For now, use mock data to demonstrate UI functionality
                var order = AvailableOrders.FirstOrDefault(o => o.Id == orderId);
                if (order != null)
                {
                    SelectedOrderNumber = order.OrderNumber;
                    CustomerName = order.CustomerName;
                    CustomerAddress = "123 Customer St, City, State 12345";
                    TotalAmount = order.TotalAmount;
                    
                    // Calculate tax and discount
                    TaxAmount = TotalAmount * 0.1m; // 10% tax
                    DiscountAmount = 0; // No discount for now
                    Subtotal = TotalAmount - TaxAmount + DiscountAmount;
                    BalanceAmount = TotalAmount - PaidAmount;

                    // Load order lines
                    var lines = new List<InvoicePostingLineViewModel>
                    {
                        new InvoicePostingLineViewModel { Description = "Product 1", Quantity = 10, UnitPrice = 75.00m, LineTotal = 750.00m },
                        new InvoicePostingLineViewModel { Description = "Product 2", Quantity = 5, UnitPrice = 150.00m, LineTotal = 750.00m }
                    };

                    InvoiceLines.Clear();
                    foreach (var line in lines)
                    {
                        InvoiceLines.Add(line);
                    }
                }
            }
            catch (Exception ex)
            {
                ErrorMessage = "Failed to load order details: " + ex.Message;
            }
            finally
            {
                IsLoading = false;
            }
        }

        private void ClearOrderDetails()
        {
            SelectedOrderNumber = string.Empty;
            CustomerName = string.Empty;
            CustomerAddress = string.Empty;
            InvoiceNumber = string.Empty;
            Subtotal = 0;
            TaxAmount = 0;
            DiscountAmount = 0;
            TotalAmount = 0;
            PaidAmount = 0;
            BalanceAmount = 0;
            InvoiceLines.Clear();
            IsLocked = false;
        }

        private void GenerateInvoiceNumber()
        {
            try
            {
                // Generate sequential invoice number
                var timestamp = DateTime.Now.ToString("yyyyMMddHHmmss");
                InvoiceNumber = $"INV-{timestamp}";
            }
            catch (Exception ex)
            {
                ErrorMessage = "Failed to generate invoice number: " + ex.Message;
            }
        }

        public async Task PostInvoiceAsync()
        {
            try
            {
                IsLoading = true;
                ErrorMessage = string.Empty;
                ValidationErrors.Clear();

                // Validate invoice
                var validationErrors = ValidateInvoice();
                if (validationErrors.Any())
                {
                    ValidationErrors = new ObservableCollection<InvoiceValidationError>(validationErrors);
                    return;
                }

                // Create invoice posting request
                var request = new InvoicePostingUiRequest
                {
                    InvoiceNumber = InvoiceNumber,
                    OrderId = SelectedOrderId,
                    CustomerName = CustomerName,
                    CustomerAddress = CustomerAddress,
                    InvoiceDate = InvoiceDate,
                    DueDate = DueDate,
                    Subtotal = Subtotal,
                    TaxAmount = TaxAmount,
                    DiscountAmount = DiscountAmount,
                    TotalAmount = TotalAmount,
                    Lines = InvoiceLines.Select(line => new InvoiceLineUiRequest
                    {
                        Description = line.Description,
                        Quantity = line.Quantity,
                        UnitPrice = line.UnitPrice,
                        LineTotal = line.LineTotal
                    }).ToList()
                };

                // Call adapter (this would be implemented in the InvoiceUiAdapter)
                // For now, simulate the posting
                await Task.Delay(100); // Simulate async operation
                
                IsLocked = true;
                
                // Show success message
                System.Windows.MessageBox.Show(
                    $"Invoice posted successfully!\n\nInvoice Number: {InvoiceNumber}\nTotal Amount: {TotalAmount:C}",
                    "Success",
                    System.Windows.MessageBoxButton.OK,
                    System.Windows.MessageBoxImage.Information);
                
                // Close window on success
                System.Windows.Application.Current.Windows.OfType<System.Windows.Window>()
                    .FirstOrDefault(w => w.DataContext == this)?.Close();
            }
            catch (Exception ex)
            {
                ErrorMessage = "Failed to post invoice: " + ex.Message;
            }
            finally
            {
                IsLoading = false;
            }
        }

        private List<InvoiceValidationError> ValidateInvoice()
        {
            var errors = new List<InvoiceValidationError>();

            if (SelectedOrderId <= 0)
                errors.Add(new InvoiceValidationError { PropertyName = "Order", ErrorMessage = "Please select a sales order" });

            if (string.IsNullOrWhiteSpace(InvoiceNumber))
                errors.Add(new InvoiceValidationError { PropertyName = "InvoiceNumber", ErrorMessage = "Invoice number is required" });

            if (string.IsNullOrWhiteSpace(CustomerName))
                errors.Add(new InvoiceValidationError { PropertyName = "Customer", ErrorMessage = "Customer name is required" });

            if (InvoiceDate > DueDate)
                errors.Add(new InvoiceValidationError { PropertyName = "DueDate", ErrorMessage = "Due date must be after invoice date" });

            if (!InvoiceLines.Any())
                errors.Add(new InvoiceValidationError { PropertyName = "Lines", ErrorMessage = "Invoice must have at least one line item" });

            if (TotalAmount <= 0)
                errors.Add(new InvoiceValidationError { PropertyName = "TotalAmount", ErrorMessage = "Total amount must be greater than 0" });

            // Check accounting balance
            var debitTotal = TotalAmount;
            var creditTotal = TaxAmount + DiscountAmount;
            if (Math.Abs(debitTotal - creditTotal) > 0.01m)
            {
                errors.Add(new InvoiceValidationError { PropertyName = "Balance", ErrorMessage = $"Accounting entries do not balance. Debit: {debitTotal:C}, Credit: {creditTotal:C}" });
            }

            return errors;
        }

        public event PropertyChangedEventHandler? PropertyChanged;

        protected virtual void OnPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
            
            // Notify computed properties when dependencies change
            if (propertyName == nameof(InvoiceLines) || propertyName == nameof(TaxAmount) || propertyName == nameof(DiscountAmount))
            {
                OnPropertyChanged(nameof(Subtotal));
                OnPropertyChanged(nameof(TotalAmount));
                OnPropertyChanged(nameof(BalanceAmount));
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

    public class InvoicePostingLineViewModel : INotifyPropertyChanged
    {
        private string _description = string.Empty;
        private decimal _quantity;
        private decimal _unitPrice;
        private decimal _lineTotal;

        public string Description
        {
            get => _description;
            set => SetProperty(ref _description, value, nameof(Description));
        }

        public decimal Quantity
        {
            get => _quantity;
            set
            {
                if (SetProperty(ref _quantity, value, nameof(Quantity)))
                {
                    UpdateLineTotal();
                }
            }
        }

        public decimal UnitPrice
        {
            get => _unitPrice;
            set
            {
                if (SetProperty(ref _unitPrice, value, nameof(UnitPrice)))
                {
                    UpdateLineTotal();
                }
            }
        }

        public decimal LineTotal
        {
            get => _lineTotal;
            set => SetProperty(ref _lineTotal, value, nameof(LineTotal));
        }

        private void UpdateLineTotal()
        {
            LineTotal = Quantity * UnitPrice;
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

    public class PaymentRegistrationViewModel : INotifyPropertyChanged
    {
        private readonly IPaymentUiAdapter _paymentAdapter;
        
        private string _invoiceNumber = string.Empty;
        private decimal _totalAmount;
        private decimal _paidAmount;
        private decimal _balanceAmount;
        private decimal _paymentAmount;
        private string _paymentMethod = "Cash";
        private string _referenceNumber = string.Empty;
        private string _notes = string.Empty;
        private bool _isLoading = false;
        private string _errorMessage = string.Empty;
        private ObservableCollection<PaymentValidationError> _validationErrors = new();
        private ObservableCollection<PaymentLookupDto> _availableInvoices = new();
        private ICommand? _loadInvoiceCommand;
        private ICommand? _registerPaymentCommand;
        private ICommand? _refreshCommand;

        public PaymentRegistrationViewModel(IPaymentUiAdapter paymentAdapter)
        {
            _paymentAdapter = paymentAdapter;
            InitializeCommands();
            _ = LoadAvailableInvoicesAsync(); // Fire and forget for constructor
        }

        public string InvoiceNumber
        {
            get => _invoiceNumber;
            set => SetProperty(ref _invoiceNumber, value, nameof(InvoiceNumber));
        }

        public decimal TotalAmount
        {
            get => _totalAmount;
            set => SetProperty(ref _totalAmount, value, nameof(TotalAmount));
        }

        public decimal PaidAmount
        {
            get => _paidAmount;
            set => SetProperty(ref _paidAmount, value, nameof(PaidAmount));
        }

        public decimal BalanceAmount
        {
            get => _balanceAmount;
            set => SetProperty(ref _balanceAmount, value, nameof(BalanceAmount));
        }

        public decimal PaymentAmount
        {
            get => _paymentAmount;
            set
            {
                if (SetProperty(ref _paymentAmount, value, nameof(PaymentAmount)))
                {
                    // Ensure payment doesn't exceed balance
                    if (PaymentAmount > BalanceAmount)
                    {
                        PaymentAmount = BalanceAmount;
                    }
                }
            }
        }

        public string PaymentMethod
        {
            get => _paymentMethod;
            set => SetProperty(ref _paymentMethod, value, nameof(PaymentMethod));
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

        public ObservableCollection<PaymentValidationError> ValidationErrors
        {
            get => _validationErrors;
            set => SetProperty(ref _validationErrors, value, nameof(ValidationErrors));
        }

        public ObservableCollection<PaymentLookupDto> AvailableInvoices
        {
            get => _availableInvoices;
            set => SetProperty(ref _availableInvoices, value, nameof(AvailableInvoices));
        }

        public ICommand LoadInvoiceCommand => _loadInvoiceCommand ??= new BestFlex.Shell.Infrastructure.RelayCommand(async () => await LoadInvoiceDetailsAsync(InvoiceNumber));
        public ICommand RegisterPaymentCommand => _registerPaymentCommand ??= new BestFlex.Shell.Infrastructure.RelayCommand(async () => await RegisterPaymentAsync(), () => PaymentAmount > 0);
        public ICommand RefreshCommand => _refreshCommand ??= new BestFlex.Shell.Infrastructure.RelayCommand(async () => await LoadAvailableInvoicesAsync());

        private void InitializeCommands()
        {
            _loadInvoiceCommand = new BestFlex.Shell.Infrastructure.RelayCommand(async () => await LoadInvoiceDetailsAsync(InvoiceNumber));
            _registerPaymentCommand = new BestFlex.Shell.Infrastructure.RelayCommand(async () => await RegisterPaymentAsync(), () => PaymentAmount > 0);
            _refreshCommand = new BestFlex.Shell.Infrastructure.RelayCommand(async () => await LoadAvailableInvoicesAsync());
        }

        public async Task LoadAvailableInvoicesAsync()
        {
            try
            {
                IsLoading = true;
                ErrorMessage = string.Empty;

                // TODO: Load available invoices from service
                // For now, use mock data to demonstrate UI functionality
                var invoices = new List<PaymentLookupDto>
                {
                    new PaymentLookupDto { InvoiceNumber = "INV-202401270001", CustomerName = "Customer 1", TotalAmount = 1500.00m, PaidAmount = 500.00m, BalanceAmount = 1000.00m, DueDate = DateTime.Now.AddDays(15) },
                    new PaymentLookupDto { InvoiceNumber = "INV-202401270002", CustomerName = "Customer 2", TotalAmount = 2500.00m, PaidAmount = 0.00m, BalanceAmount = 2500.00m, DueDate = DateTime.Now.AddDays(20) },
                    new PaymentLookupDto { InvoiceNumber = "INV-202401270003", CustomerName = "Customer 3", TotalAmount = 800.00m, PaidAmount = 800.00m, BalanceAmount = 0.00m, DueDate = DateTime.Now.AddDays(10) }
                };

                AvailableInvoices.Clear();
                foreach (var invoice in invoices)
                {
                    AvailableInvoices.Add(invoice);
                }
            }
            catch (Exception ex)
            {
                ErrorMessage = "Failed to load available invoices: " + ex.Message;
            }
            finally
            {
                IsLoading = false;
            }
        }

        public async Task LoadInvoiceDetailsAsync(string invoiceNumber)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(invoiceNumber))
                {
                    ClearInvoiceDetails();
                    return;
                }

                IsLoading = true;
                ErrorMessage = string.Empty;

                var invoice = AvailableInvoices.FirstOrDefault(i => i.InvoiceNumber == invoiceNumber);
                if (invoice != null)
                {
                    InvoiceNumber = invoice.InvoiceNumber;
                    TotalAmount = invoice.TotalAmount;
                    PaidAmount = invoice.PaidAmount;
                    BalanceAmount = invoice.BalanceAmount;
                    PaymentAmount = Math.Min(BalanceAmount, 0); // Default to balance amount
                }
            }
            catch (Exception ex)
            {
                ErrorMessage = "Failed to load invoice details: " + ex.Message;
            }
            finally
            {
                IsLoading = false;
            }
        }

        private void ClearInvoiceDetails()
        {
            InvoiceNumber = string.Empty;
            TotalAmount = 0;
            PaidAmount = 0;
            BalanceAmount = 0;
            PaymentAmount = 0;
            PaymentMethod = "Cash";
            ReferenceNumber = string.Empty;
            Notes = string.Empty;
        }

        public async Task RegisterPaymentAsync()
        {
            try
            {
                IsLoading = true;
                ErrorMessage = string.Empty;
                ValidationErrors.Clear();

                // Validate payment
                var validationErrors = ValidatePayment();
                if (validationErrors.Any())
                {
                    ValidationErrors = new ObservableCollection<PaymentValidationError>(validationErrors);
                    return;
                }

                // Create payment registration request
                var request = new PaymentRegistrationUiRequest
                {
                    InvoiceNumber = InvoiceNumber,
                    PaymentAmount = PaymentAmount,
                    PaymentMethod = PaymentMethod,
                    ReferenceNumber = string.IsNullOrWhiteSpace(ReferenceNumber) ? $"PAY-{DateTime.Now:yyyyMMddHHmmss}" : ReferenceNumber,
                    Notes = Notes
                };

                // Call adapter (this would be implemented in the PaymentUiAdapter)
                // For now, simulate the payment registration
                await Task.Delay(100); // Simulate async operation
                
                // Update paid amount and balance
                PaidAmount += PaymentAmount;
                BalanceAmount -= PaymentAmount;

                // Show success message
                System.Windows.MessageBox.Show(
                    $"Payment registered successfully!\n\nInvoice: {InvoiceNumber}\nPayment Amount: {PaymentAmount:C}\nNew Balance: {BalanceAmount:C}",
                    "Success",
                    System.Windows.MessageBoxButton.OK,
                    System.Windows.MessageBoxImage.Information);
                
                // Clear form for next payment
                PaymentAmount = 0;
                ReferenceNumber = string.Empty;
                Notes = string.Empty;
            }
            catch (Exception ex)
            {
                ErrorMessage = "Failed to register payment: " + ex.Message;
            }
            finally
            {
                IsLoading = false;
            }
        }

        private List<PaymentValidationError> ValidatePayment()
        {
            var errors = new List<PaymentValidationError>();

            if (string.IsNullOrWhiteSpace(InvoiceNumber))
                errors.Add(new PaymentValidationError { PropertyName = "Invoice", ErrorMessage = "Please select an invoice" });

            if (PaymentAmount <= 0)
                errors.Add(new PaymentValidationError { PropertyName = "PaymentAmount", ErrorMessage = "Payment amount must be greater than 0" });

            if (PaymentAmount > BalanceAmount)
                errors.Add(new PaymentValidationError { PropertyName = "PaymentAmount", ErrorMessage = $"Payment amount ({PaymentAmount:C}) exceeds balance ({BalanceAmount:C})" });

            if (string.IsNullOrWhiteSpace(PaymentMethod))
                errors.Add(new PaymentValidationError { PropertyName = "PaymentMethod", ErrorMessage = "Payment method is required" });

            return errors;
        }

        public event PropertyChangedEventHandler? PropertyChanged;

        protected virtual void OnPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
            
            // Notify computed properties when dependencies change
            if (propertyName == nameof(PaidAmount) || propertyName == nameof(BalanceAmount))
            {
                OnPropertyChanged(nameof(PaymentAmount));
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

    // Mock DTOs for demonstration
    public class SalesOrderLookupDto
    {
        public int Id { get; set; }
        public string OrderNumber { get; set; } = string.Empty;
        public string CustomerName { get; set; } = string.Empty;
        public decimal TotalAmount { get; set; }
        public string Status { get; set; } = string.Empty;
        public DateTime OrderDate { get; set; }
    }

    public class PaymentLookupDto
    {
        public string InvoiceNumber { get; set; } = string.Empty;
        public string CustomerName { get; set; } = string.Empty;
        public decimal TotalAmount { get; set; }
        public decimal PaidAmount { get; set; }
        public decimal BalanceAmount { get; set; }
        public DateTime DueDate { get; set; }
    }

    public class InvoiceValidationError
    {
        public string PropertyName { get; set; } = string.Empty;
        public string ErrorMessage { get; set; } = string.Empty;
    }

    public class PaymentValidationError
    {
        public string PropertyName { get; set; } = string.Empty;
        public string ErrorMessage { get; set; } = string.Empty;
    }

    // Mock DTOs for UI requests
    public class InvoicePostingUiRequest
    {
        public string InvoiceNumber { get; set; } = string.Empty;
        public int OrderId { get; set; }
        public string CustomerName { get; set; } = string.Empty;
        public string CustomerAddress { get; set; } = string.Empty;
        public DateTime InvoiceDate { get; set; }
        public DateTime DueDate { get; set; }
        public decimal Subtotal { get; set; }
        public decimal TaxAmount { get; set; }
        public decimal DiscountAmount { get; set; }
        public decimal TotalAmount { get; set; }
        public List<InvoiceLineUiRequest> Lines { get; set; } = new();
    }

    public class InvoiceLineUiRequest
    {
        public string Description { get; set; } = string.Empty;
        public decimal Quantity { get; set; }
        public decimal UnitPrice { get; set; }
        public decimal LineTotal { get; set; }
    }

    public class PaymentRegistrationUiRequest
    {
        public string InvoiceNumber { get; set; } = string.Empty;
        public decimal PaymentAmount { get; set; }
        public string PaymentMethod { get; set; } = string.Empty;
        public string ReferenceNumber { get; set; } = string.Empty;
        public string Notes { get; set; } = string.Empty;
    }

    public interface IPaymentUiAdapter
    {
        Task<PaymentRegistrationResult> RegisterPaymentAsync(PaymentRegistrationUiRequest request, System.Threading.CancellationToken cancellationToken = default);
    }

    public class PaymentRegistrationResult
    {
        public bool Success { get; set; }
        public string ErrorMessage { get; set; } = string.Empty;
        public string UserFriendlyMessage { get; set; } = string.Empty;
        public List<PaymentValidationError> ValidationErrors { get; set; } = new();
    }
}
