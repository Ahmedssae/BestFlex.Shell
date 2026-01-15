using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using BestFlex.Application.Abstractions;
using BestFlex.Application.Contracts.Sales;
using BestFlex.Persistence.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;


namespace BestFlex.Shell.ViewModels
{
    public sealed class NewSaleViewModel : ViewModelBase
    {
        private readonly ISalesService _sales;
        private readonly IServiceProvider _sp;
        private bool _isBusy;
        private int? _lastInvoiceId;

        public NewSaleViewModel(IServiceProvider sp, ISalesService sales)
        {
            _sp = sp ?? throw new ArgumentNullException(nameof(sp));
            _sales = sales ?? throw new ArgumentNullException(nameof(sales));
            SaveCommand = new AsyncRelayCommand(SaveAsync, () => CanSave && !IsBusy);
            AddLineCommand = new DelegateCommand(async () => await AddLineAsync());
            RemoveLineCommand = new DelegateCommand<SaleLineVm>(vm => RemoveLine(vm));
            RecalculateCommand = new DelegateCommand(() => RecalculateSubtotal());
        // listen for collection changes to update totals automatically
        Lines.CollectionChanged += Lines_CollectionChanged;
        // Listen for selected customer changes if we had a property; expose SelectedCustomerId
        }

        private int? _selectedCustomerId;
        public int? SelectedCustomerId { get => _selectedCustomerId; set { if (SetProperty(ref _selectedCustomerId, value)) OnValidationChanged(); } }

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

        public AsyncRelayCommand SaveCommand { get; }
        public ICommand AddLineCommand { get; }
        public ICommand RemoveLineCommand { get; }
        public ICommand RecalculateCommand { get; }

        public bool CanSave => SelectedCustomerId.HasValue && Lines.Any() && Lines.All(l => l.ProductId > 0 && l.Quantity > 0 && l.UnitPrice >= 0);

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

        public async Task LoadAsync() => await Task.CompletedTask;

        // Discount / tax properties (defaults zero)
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

        public async Task LoadLookupsAsync()
        {
            try
            {
                using var scope = _sp.CreateScope();
                var db = scope.ServiceProvider.GetRequiredService<BestFlexDbContext>();

                var customers = await db.CustomerAccounts
                    .AsNoTracking()
                    .OrderBy(c => c.Name)
                    .Select(c => new CustomerItem { Id = c.Id, Name = c.Name })
                    .ToListAsync();

                Customers.Clear();
                foreach (var c in customers) Customers.Add(c);

                var prods = await db.Products
                    .AsNoTracking()
                    .OrderBy(p => p.Code)
                    .Select(p => new { p.Id, p.Code, p.Name, p.StockQty })
                    .ToListAsync();

                Products.Clear();
                foreach (var p in prods)
                {
                    var price = await TryResolvePriceAsync(p.Id);
                    Products.Add(new ProductVm
                    {
                        Id = p.Id,
                        Code = p.Code,
                        Name = p.Name,
                        StockQty = p.StockQty,
                        DefaultPrice = price ?? 0m
                    });
                }
            }
            finally { await Task.CompletedTask; }
        }

        // Lightweight command implementation for simple synchronous actions
        private sealed class DelegateCommand : ICommand
        {
            private readonly Action _act;
            public DelegateCommand(Action act) => _act = act;
            public bool CanExecute(object? p) => true;
            public void Execute(object? p) => _act();
            // Explicit add/remove to avoid unused-event warnings
            event EventHandler? ICommand.CanExecuteChanged { add { } remove { } }
        }
        private sealed class DelegateCommand<T> : ICommand
        {
            private readonly Action<T> _act;
            public DelegateCommand(Action<T> act) => _act = act;
            public bool CanExecute(object? p) => true;
            public void Execute(object? p) => _act((T)p!);
            // Explicit add/remove to avoid unused-event warnings
            event EventHandler? ICommand.CanExecuteChanged { add { } remove { } }
        }

        private async Task<decimal?> TryResolvePriceAsync(int productId)
        {
            using var scope = _sp.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<BestFlexDbContext>();

            var p = await db.Products.AsNoTracking().FirstOrDefaultAsync(x => x.Id == productId);
            if (p == null) return null;

            var t = p.GetType();
            var priceProp = t.GetProperty("DefaultPrice")
                          ?? t.GetProperty("SellingPrice")
                          ?? t.GetProperty("Price");
            if (priceProp != null && priceProp.PropertyType == typeof(decimal))
                return (decimal)priceProp.GetValue(p)!;

            return null;
        }

        public async Task AddLineAsync()
        {
            var line = new SaleLineVm(this);
            Lines.Add(line);
            // subscription will be handled by CollectionChanged handler, but ensure totals updated
            await Task.CompletedTask;
            RecalculateSubtotal();
            OnValidationChanged();
        }

        public void RemoveLine(SaleLineVm vm)
        {
            if (vm == null) return;
            Lines.Remove(vm);
            // Recalculate will be triggered by collection change handler; ensure update
            RecalculateSubtotal();
            OnValidationChanged();
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
            if (IsBusy) return;

            var dto = new NewSaleDto
            {
                CustomerId = CustomerId,
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

            try
            {
                IsBusy = true;
                var invoiceId = await _sales.CreateSaleAsync(dto);
                LastInvoiceId = invoiceId;

                // Reset for next sale (or navigate to Invoice Details)
                Lines.Clear();
                Notes = null;
                OnPropertyChanged(nameof(Notes));
            }
            finally
            {
                IsBusy = false;
            }
        }
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
                }
            }
        }

        private string _productName = "";
        public string ProductName { get => _productName; private set => SetProperty(ref _productName, value); }

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
