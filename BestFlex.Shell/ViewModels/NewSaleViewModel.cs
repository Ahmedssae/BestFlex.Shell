using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using BestFlex.Application.Abstractions;
using BestFlex.Application.Contracts.Sales;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace BestFlex.Shell.ViewModels
{
    public sealed class NewSaleViewModel : ViewModelBase
    {
        private readonly ISalesService _sales;
        private readonly IServiceProvider _sp;
        private readonly IPermissionService _permissions;
        private readonly IAuditService _audit;
        private readonly IErrorService _error;
        private readonly ILogger<NewSaleViewModel> _logger;
        private readonly IAuthorizationService _authorization;
        private bool _isBusy;
        private int? _lastInvoiceId;
        private readonly SemaphoreSlim _loadLock = new(1, 1);
        private bool _isFeatureAvailable;
        private string? _featureUnavailableReason;
        private bool _isExecuting;
        private bool _canCreateSale;

        public Guid OperationId { get; } = Guid.NewGuid();

        public bool IsExecuting
        {
            get => _isExecuting;
            private set => SetProperty(ref _isExecuting, value);
        }

        public bool CanCreateSale => _canCreateSale && !IsBusy && !IsExecuting;

        private bool CanSave() => SelectedCustomerId.HasValue && Lines.All(l => l.ProductId > 0 && l.Quantity > 0 && l.UnitPrice >= 0) && !IsExecuting && !IsBusy && HasCreateSalePermission;

        public NewSaleViewModel(
            IServiceProvider sp,
            ISalesService sales,
            IPermissionService permissions,
            IAuditService audit,
            IErrorService error,
            ILogger<NewSaleViewModel> logger,
            IAuthorizationService authorization)
        {
            // CORE REQUIRED dependencies - no GetService, no null checks
            _sp = sp ?? throw new ArgumentNullException(nameof(sp));
            _sales = sales ?? throw new ArgumentNullException(nameof(sales));
            _permissions = permissions ?? throw new ArgumentNullException(nameof(permissions));
            _audit = audit ?? throw new ArgumentNullException(nameof(audit));
            _error = error ?? throw new ArgumentNullException(nameof(error));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _authorization = authorization ?? throw new ArgumentNullException(nameof(authorization));
                
                // Initialize commands (READ-ONLY operations only)
                SaveCommand = new AsyncRelayCommand(SaveAsync, () => CanSave());
                AddLineCommand = new AsyncRelayCommand(AddLineAsync, () => !IsBusy && HasCreateSalePermission && SelectedProduct != null && Qty > 0);
                RemoveLineCommand = new AsyncRelayCommand<SaleLineVm>(RemoveLine, _ => !IsBusy && HasCreateSalePermission);
                RecalculateCommand = new AsyncRelayCommand(() => { RecalculateSubtotal(); return Task.CompletedTask; }, () => !IsBusy && HasCreateSalePermission);
                
                // listen for collection changes to update totals automatically
                Lines.CollectionChanged += Lines_CollectionChanged;
        }

        private CustomerItem? _selectedCustomer;
        private int? _selectedCustomerId;
        
        public CustomerItem? SelectedCustomer 
        { 
            get => _selectedCustomer; 
            set 
            { 
                if (SetProperty(ref _selectedCustomer, value)) 
                {
                    SelectedCustomerId = value?.Id;
                    OnValidationChanged();
                }
            } 
        }
        
        public int? SelectedCustomerId 
        { 
            get => _selectedCustomerId; 
            set 
            { 
                if (SetProperty(ref _selectedCustomerId, value)) 
                {
                    OnValidationChanged();
                }
            } 
        }

        private ProductVm? _selectedProduct;
        public ProductVm? SelectedProduct
        {
            get => _selectedProduct;
            set
            {
                if (SetProperty(ref _selectedProduct, value))
                {
                    // When product changes, update current line or create new one
                    if (value != null && Lines.Any())
                    {
                        var lastLine = Lines.Last();
                        lastLine.ProductId = value.Id;
                        // ProductName will be updated automatically via ProductId setter
                    }
                    // Update command CanExecute
                    AddLineCommand?.RaiseCanExecuteChanged();
                }
            }
        }

        public string? ProductInput { get; set; }
        
        private decimal _qty = 1m;
        public decimal Qty 
        { 
            get => _qty; 
            set 
            { 
                if (SetProperty(ref _qty, value)) 
                {
                    // When qty changes, update the current line if exists
                    if (Lines.Any())
                    {
                        var lastLine = Lines.Last();
                        lastLine.Quantity = value;
                    }
                    // Update command CanExecute
                    AddLineCommand?.RaiseCanExecuteChanged();
                }
            } 
        }

        public int? CustomerId { get; set; }
        public DateTime InvoiceDate { get; set; } = DateTime.Now;
        public string Currency { get; set; } = "USD";
        public string? Notes { get; set; }

        public ObservableCollection<SaleLineVm> Lines { get; } = new();

        public ObservableCollection<ProductVm> Products { get; } = new();
        public ObservableCollection<CustomerItem> Customers { get; } = new();

        private decimal _subtotal;
        public decimal Subtotal { get => _subtotal; private set { SetProperty(ref _subtotal, value); } }

        public int ItemsCount => (int)Lines.Sum(l => l.Quantity);

        public bool IsBusy
        {
            get => _isBusy;
            private set
            {
                if (SetProperty(ref _isBusy, value))
                {
                    // ensure command state reflects busy flag
                    SaveCommand?.RaiseCanExecuteChanged();
                }
            }
        }

        public int? LastInvoiceId { get => _lastInvoiceId; private set => SetProperty(ref _lastInvoiceId, value); }

        public bool IsFeatureAvailable
        {
            get => _isFeatureAvailable;
            private set => SetProperty(ref _isFeatureAvailable, value);
        }

        public string? FeatureUnavailableReason
        {
            get => _featureUnavailableReason;
            private set => SetProperty(ref _featureUnavailableReason, value);
        }

        // Permission properties
        public bool HasCreateSalePermission => _authorization.HasPermissionAsync(Permission.CreateSale).Result;
        
        public AsyncRelayCommand SaveCommand { get; }
        public AsyncRelayCommand AddLineCommand { get; }
        public AsyncRelayCommand<SaleLineVm> RemoveLineCommand { get; }
        public AsyncRelayCommand RecalculateCommand { get; }

        private void OnValidationChanged()
        {
            // notify bindings
            OnPropertyChanged(nameof(CanSave));
            // raise command state
            SaveCommand?.RaiseCanExecuteChanged();
            // update validation message
            var msg = ComputeValidationMessage();
            if (!string.Equals(_validationMessage, msg, StringComparison.Ordinal))
            {
                SetProperty(ref _validationMessage, msg, nameof(ValidationMessage));
            }
        }

        private string? _validationMessage;
        public string? ValidationMessage { get => _validationMessage; }

        private string? ComputeValidationMessage()
        {
            if (!SelectedCustomerId.HasValue)
                return "Select a customer";
            if (!Lines.Any())
                return "Add at least one product";
            if (Lines.Any(l => l.ProductId <= 0 || l.Quantity <= 0 || l.UnitPrice < 0))
                return "Fix invalid quantities or prices";
            return null;
        }

        public async Task InitializeAsync() 
        { 
            try
            {
                _logger?.LogInformation("NewSaleViewModel.InitializeAsync started for OperationId: {OperationId}", OperationId);
                
                // Validate authorization FIRST
                var hasPermission = await _authorization.HasPermissionAsync(Permission.CreateSale);
                if (!hasPermission)
                {
                    var reason = await _authorization.GetPermissionDeniedReasonAsync(Permission.CreateSale);
                    throw new UserFriendlyException(reason ?? "You do not have permission to create sales.");
                }
                
                _canCreateSale = true; // Set permission flag
                
                // Check feature availability (moved from constructor)
                CheckFeatureAvailability();
                
                if (!IsFeatureAvailable)
                {
                    throw new InvalidOperationException(FeatureUnavailableReason ?? "Sales feature not available");
                }
                
                await LoadLookupsAsync();
                
                _logger?.LogInformation("NewSaleViewModel.InitializeAsync completed for OperationId: {OperationId}", OperationId);
            }
            catch (UserFriendlyException)
            {
                // Re-throw user-friendly exceptions as-is
                throw;
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "NewSaleViewModel.InitializeAsync failed for OperationId: {OperationId}", OperationId);
                throw new UserFriendlyException($"Failed to initialize sales module: {ex.Message}", ex);
            }
        }

        public async Task LoadLookupsAsync()
        {
            if (IsBusy) return;
            
            await _loadLock.WaitAsync();
            try
            {
                if (IsBusy) return; // Double-check pattern
                IsBusy = true;
                
                using var scope = _sp.CreateScope();
                var productReadService = scope.ServiceProvider.GetRequiredService<IProductReadService>();
                var customerReadService = scope.ServiceProvider.GetRequiredService<ICustomerReadService>();
                
                // Load customers using read service
                var customers = await customerReadService.GetForSalesAsync();
                Customers.Clear();
                foreach (var c in customers) 
                {
                    Customers.Add(new CustomerItem { Id = c.Id, Name = c.Name });
                }
                _logger.LogInformation("Loaded {Count} customers for sales", Customers.Count);
                
                if (!Customers.Any())
                {
                    _logger.LogWarning("No customers available for sales");
                    _error.HandleUserError("No customers available. Please create customers first.", "Data Unavailable");
                }

                // Load products using read service
                var products = await productReadService.GetForSalesAsync();
                Products.Clear();
                foreach (var p in products)
                {
                    Products.Add(new ProductVm
                    {
                        Id = p.Id,
                        Code = p.Code,
                        Name = p.Name,
                        StockQty = p.StockQty,
                        DefaultPrice = p.Price
                    });
                }
                _logger.LogInformation("Loaded {Count} products for sales", Products.Count);
                
                if (!Products.Any())
                {
                    _logger.LogWarning("No products available for sales");
                    _error.HandleUserError("No products available. Please add products to inventory first.", "Data Unavailable");
                }
            }
            catch (Exception ex)
            {
                _error.Handle(ex, "NewSaleViewModel.LoadLookupsAsync");
            }
            finally
            {
                IsBusy = false;
                _loadLock.Release();
            }
        }

        public async Task AddLineAsync()
        {
            try
            {
                _logger.LogInformation("AddLineAsync started");
                
                if (!IsFeatureAvailable)
                {
                    throw new InvalidOperationException(FeatureUnavailableReason ?? "Sales feature not available");
                }
                
                if (SelectedProduct == null) return;
                
                var line = new SaleLineVm(this);
                line.ProductId = SelectedProduct.Id;
                line.Quantity = Qty;
                line.UnitPrice = SelectedProduct.DefaultPrice;
                
                Lines.Add(line);
                
                // Reset selection for next line
                SelectedProduct = null;
                Qty = 1m;
                
                // subscription will be handled by CollectionChanged handler, but ensure totals updated
                await Task.CompletedTask;
                RecalculateSubtotal();
                OnValidationChanged();
                
                _logger.LogInformation("AddLineAsync completed successfully");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "AddLineAsync failed");
                _error.Handle(ex, "Failed to add line");
            }
        }

        public Task RemoveLine(SaleLineVm vm)
        {
            if (vm == null) return Task.CompletedTask;
            Lines.Remove(vm);
            // Recalculate will be triggered by collection change handler; ensure update
            RecalculateSubtotal();
            OnValidationChanged();
            return Task.CompletedTask;
        }

        internal void OnLineChanged() => RecalculateSubtotal();

        private void RecalculateSubtotal()
        {
            var subtotal = Math.Round(Lines.Sum(l => l.LineTotal), 2, MidpointRounding.AwayFromZero);
            Subtotal = subtotal;
            // Discount amount
            DiscountAmount = DiscountPercent > 0 ? Math.Round(subtotal * (DiscountPercent / 100m), 2) : 0m;
            var taxableBase = subtotal - DiscountAmount;
            TaxAmount = TaxPercent > 0 ? Math.Round(taxableBase * (TaxPercent / 100m), 2) : 0m;
            Total = taxableBase + TaxAmount;

            OnPropertyChanged(nameof(ItemsCount));
            OnPropertyChanged(nameof(Subtotal));
            OnPropertyChanged(nameof(DiscountAmount));
            OnPropertyChanged(nameof(TaxAmount));
            OnPropertyChanged(nameof(Total));
        }

        private void Lines_CollectionChanged(object? sender, System.Collections.Specialized.NotifyCollectionChangedEventArgs e)
        {
            if (e == null) return;
            if (e.Action == System.Collections.Specialized.NotifyCollectionChangedAction.Add && e.NewItems != null)
            {
                foreach (var it in e.NewItems.OfType<SaleLineVm>()) SubscribeLine(it);
            }
            else if (e.Action == System.Collections.Specialized.NotifyCollectionChangedAction.Remove && e.OldItems != null)
            {
                foreach (var it in e.OldItems.OfType<SaleLineVm>()) UnsubscribeLine(it);
            }
            else if (e.Action == System.Collections.Specialized.NotifyCollectionChangedAction.Replace)
            {
                if (e.OldItems != null) foreach (var it in e.OldItems.OfType<SaleLineVm>()) UnsubscribeLine(it);
                if (e.NewItems != null) foreach (var it in e.NewItems.OfType<SaleLineVm>()) SubscribeLine(it);
            }
            else if (e.Action == System.Collections.Specialized.NotifyCollectionChangedAction.Reset)
            {
                // clear all subscriptions
                // best-effort: unsubscribe by iterating existing items (collection is cleared)
                // nothing to do because items removed; ensure subtotal update
            }

            RecalculateSubtotal();
            // Update SaveCommand CanExecute when Lines collection changes
            SaveCommand?.RaiseCanExecuteChanged();
            OnValidationChanged();
        }

        private void SubscribeLine(SaleLineVm line)
        {
            if (line == null) return;
            line.PropertyChanged += Line_PropertyChanged;
            // also listen for validation-relevant changes
            line.PropertyChanged += Line_ValidationPropertyChanged;
        }

        private void UnsubscribeLine(SaleLineVm line)
        {
            if (line == null) return;
            line.PropertyChanged -= Line_PropertyChanged;
            line.PropertyChanged -= Line_ValidationPropertyChanged;
        }

        private void Line_PropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
        {
            if (e == null) return;
            // respond to changes that affect totals
            if (e.PropertyName == nameof(SaleLineVm.Quantity) || e.PropertyName == nameof(SaleLineVm.UnitPrice) || e.PropertyName == nameof(SaleLineVm.LineTotal))
            {
                RecalculateSubtotal();
            }
        }

        private void Line_ValidationPropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
        {
            if (e == null) return;
            if (e.PropertyName == nameof(SaleLineVm.Quantity) || e.PropertyName == nameof(SaleLineVm.UnitPrice) || e.PropertyName == nameof(SaleLineVm.ProductId))
            {
                OnValidationChanged();
            }
        }

        public async Task SaveAsync()
        {
            try
            {
                _logger.LogInformation("SaveAsync started");
                
                if (!IsFeatureAvailable)
                {
                    throw new InvalidOperationException(FeatureUnavailableReason ?? "Sales feature not available");
                }
                
                if (!CanSave()) return;
                
                IsBusy = true;
                IsExecuting = true;
                
                var dto = new NewSaleDto
                {
                    CustomerId = SelectedCustomerId,
                    InvoiceDate = InvoiceDate,
                    Currency = Currency,
                    Notes = Notes,
                    Items = Lines.Select(l => new NewSaleItemDto
                    {
                        ProductId = l.ProductId,
                        Quantity = l.Quantity,
                        UnitPrice = l.UnitPrice
                    }).ToList()
                };
                
                var invoiceId = await _sales.CreateSaleAsync(dto);
                LastInvoiceId = invoiceId;
                
                // Audit the sale creation
                await _audit.LogActionAsync("SaleCreated", "SellingInvoice", invoiceId);
                
                _logger.LogInformation("SaveAsync completed successfully with invoice ID {InvoiceId}", invoiceId);
                
                // Close window after successful save
                var window = System.Windows.Application.Current.Windows.OfType<Window>()
                    .FirstOrDefault(w => w.DataContext == this);
                window?.Close();
                Notes = null;
                OnPropertyChanged(nameof(Notes));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "NewSaleViewModel.SaveAsync failed");
                _error.Handle(ex, "Failed to save sale");
            }
            finally
            {
                IsBusy = false;
                IsExecuting = false;
            }
        }

        private void CheckFeatureAvailability()
        {
            try
            {
                // Check if required services are available
                var productReadService = _sp.GetService<IProductReadService>();
                var customerReadService = _sp.GetService<ICustomerReadService>();

                if (productReadService == null || customerReadService == null)
                {
                    IsFeatureAvailable = false;
                    FeatureUnavailableReason = "Sales services not available";
                    return;
                }

                // NOTE: Permission check moved to InitializeAsync() to avoid constructor work
                // This method now only checks service availability

                IsFeatureAvailable = true;
                FeatureUnavailableReason = null;
                _logger.LogInformation("Sales feature availability check passed");
            }
            catch (Exception ex)
            {
                var unwrapped = ReflectionExceptionUnwrapper.Unwrap(ex);
                _logger.LogError(unwrapped, "Sales feature availability check failed");
                IsFeatureAvailable = false;
                FeatureUnavailableReason = "Feature availability check failed";
            }
        }

        // Additional properties for calculations
        private decimal _discountPercent;
        public decimal DiscountPercent { get => _discountPercent; set { if (SetProperty(ref _discountPercent, value)) RecalculateSubtotal(); } }

        private decimal _taxPercent;
        public decimal TaxPercent { get => _taxPercent; set { if (SetProperty(ref _taxPercent, value)) RecalculateSubtotal(); } }

        private decimal _discountAmount;
        public decimal DiscountAmount { get => _discountAmount; private set => SetProperty(ref _discountAmount, value); }

        private decimal _taxAmount;
        public decimal TaxAmount { get => _taxAmount; private set => SetProperty(ref _taxAmount, value); }

        private decimal _total;
        public decimal Total { get => _total; private set => SetProperty(ref _total, value); }
    }

    public sealed class SaleLineVm : ViewModelBase
    {
        private readonly NewSaleViewModel _owner;

        public SaleLineVm(NewSaleViewModel owner)
        {
            _owner = owner;
        }

        private int _productId;
        public int ProductId
        {
            get => _productId;
            set
            {
                if (SetProperty(ref _productId, value))
                {
                    // auto-fill from owner's product list
                    var p = _owner.Products.FirstOrDefault(x => x.Id == _productId);
                    if (p != null)
                    {
                        ProductName = p.Name;
                        if (UnitPrice == 0m) UnitPrice = p.DefaultPrice;
                        if (Quantity == 0m) Quantity = 1m;
                    }
                    _owner.OnLineChanged();
                    OnPropertyChanged(nameof(LineTotal));
                    OnPropertyChanged(nameof(ProductCode)); // Notify ProductCode change
                }
            }
        }

        private string _productName = "";
        public string ProductName { get => _productName; private set => SetProperty(ref _productName, value); }
        
        public string ProductCode 
        { 
            get 
            { 
                var p = _owner.Products.FirstOrDefault(x => x.Id == _productId);
                return p?.Code ?? string.Empty;
            } 
        }

        private decimal _quantity;
        public decimal Quantity
        {
            get => _quantity;
            set
            {
                var v = value < 0 ? 0 : value;
                if (SetProperty(ref _quantity, v))
                {
                    _owner.OnLineChanged();
                    OnPropertyChanged(nameof(LineTotal));
                }
            }
        }

        private decimal _unitPrice;
        public decimal UnitPrice
        {
            get => _unitPrice;
            set
            {
                var v = value < 0 ? 0 : value;
                if (SetProperty(ref _unitPrice, v))
                {
                    _owner.OnLineChanged();
                    OnPropertyChanged(nameof(LineTotal));
                }
            }
        }

        public decimal LineTotal => Math.Round(Quantity * UnitPrice, 2, MidpointRounding.AwayFromZero);
    }

    public sealed class ProductVm
    {
        public int Id { get; set; }
        public string Code { get; set; } = "";
        public string Name { get; set; } = "";
        public decimal StockQty { get; set; }
        public decimal DefaultPrice { get; set; }

        public string Display => string.IsNullOrWhiteSpace(Code) ? Name : $"{Code} — {Name}";
    }

    public sealed class CustomerItem
    {
        public int Id { get; set; }
        public string Name { get; set; } = "";
    }
}
