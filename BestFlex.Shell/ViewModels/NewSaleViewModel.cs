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

        public NewSaleViewModel(IServiceProvider sp, ISalesService sales)
        {
            _sp = sp ?? throw new ArgumentNullException(nameof(sp));
            _sales = sales ?? throw new ArgumentNullException(nameof(sales));
            SaveCommand = new AsyncRelayCommand(SaveAsync, CanSave);
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

        public int ItemsCount => Lines.Sum(l => (int)l.Quantity);

        public ICommand SaveCommand { get; }

        private bool CanSave() => Lines.Any() && Lines.All(l => l.ProductId > 0 && l.Quantity > 0 && l.UnitPrice >= 0);

        public async Task LoadAsync() => await Task.CompletedTask;

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
            Lines.Add(new SaleLineVm(this));
            await Task.CompletedTask;
            RecalculateSubtotal();
        }

        public void RemoveLine(SaleLineVm vm)
        {
            if (vm == null) return;
            Lines.Remove(vm);
            RecalculateSubtotal();
        }

        internal void OnLineChanged() => RecalculateSubtotal();

        private void RecalculateSubtotal()
        {
            Subtotal = Math.Round(Lines.Sum(l => l.LineTotal), 2, MidpointRounding.AwayFromZero);
            OnPropertyChanged(nameof(ItemsCount));
            OnPropertyChanged(nameof(Subtotal));
        }

        public async Task SaveAsync()
        {
            try
            {
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

                var invoiceId = await _sales.CreateSaleAsync(dto);

                MessageBox.Show($"Sale saved. Invoice #{invoiceId}", "BestFlex", MessageBoxButton.OK, MessageBoxImage.Information);

                // Reset for next sale (or navigate to Invoice Details)
                Lines.Clear();
                Notes = null;
                OnPropertyChanged(nameof(Notes));
            }
            catch (InvalidOperationException ex)
            {
                MessageBox.Show(ex.Message, "Couldn’t save", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
            catch (Exception ex)
            {
                MessageBox.Show("Unexpected error:\n" + ex.Message, "Error", MessageBoxButton.OK, MessageBoxImage.Error);
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
                if (_productId == value) return;
                _productId = value;
                OnPropertyChanged();
                // auto-fill from owner's product list
                var p = _owner.Products.FirstOrDefault(x => x.Id == _productId);
                if (p != null)
                {
                    ProductName = p.Name;
                    UnitPrice = UnitPrice == 0m ? p.DefaultPrice : UnitPrice;
                    if (Quantity == 0m) Quantity = 1m;
                }
                _owner.OnLineChanged();
                OnPropertyChanged(nameof(LineTotal));
            }
        }

        private string _productName = "";
        public string ProductName { get => _productName; private set { _productName = value; OnPropertyChanged(); } }

        private decimal _quantity;
        public decimal Quantity { get => _quantity; set { _quantity = value < 0 ? 0 : value; OnPropertyChanged(); _owner.OnLineChanged(); OnPropertyChanged(nameof(LineTotal)); } }

        private decimal _unitPrice;
        public decimal UnitPrice { get => _unitPrice; set { _unitPrice = value < 0 ? 0 : value; OnPropertyChanged(); _owner.OnLineChanged(); OnPropertyChanged(nameof(LineTotal)); } }

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
