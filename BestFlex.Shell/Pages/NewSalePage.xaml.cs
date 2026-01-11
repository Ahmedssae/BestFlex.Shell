using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Globalization;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using BestFlex.Persistence.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace BestFlex.Shell.Pages
{
    public partial class NewSalePage : UserControl, INotifyPropertyChanged
    {
        public ObservableCollection<ProductVm> Products { get; } = new();
        public ObservableCollection<LineVm> Lines { get; } = new();

        public event PropertyChangedEventHandler? PropertyChanged;

        public NewSalePage()
        {
            InitializeComponent();
            DataContext = this;
        }

        private void Raise([CallerMemberName] string? p = null) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(p));

        private async void UserControl_Loaded(object sender, RoutedEventArgs e)
        {
            cmbCurrency.SelectedIndex = 0;
            dpDate.SelectedDate = DateTime.Today;

            await LoadLookupsAsync();
            // start with one line
            AddLine();
            UpdateTotals();
        }

        private async Task LoadLookupsAsync()
        {
            try
            {
                ShowOverlay(true);

                var sp = ((App)System.Windows.Application.Current).Services;
                using var scope = sp.CreateScope();
                var db = scope.ServiceProvider.GetRequiredService<BestFlexDbContext>();

                var customers = await db.CustomerAccounts
                    .AsNoTracking()
                    .OrderBy(c => c.Name)
                    .Select(c => new { c.Id, c.Name })
                    .ToListAsync();

                cmbCustomer.ItemsSource = customers;

                var prods = await db.Products
                    .AsNoTracking()
                    .OrderBy(p => p.Code)
                    .Select(p => new
                    {
                        p.Id,
                        p.Code,
                        p.Name,
                        p.StockQty,
                        // Try to resolve a price column (DefaultPrice / SellingPrice / Price). Use 0 when absent.
                        DefaultPrice = (decimal?)0m
                    })
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
            catch (Exception ex)
            {
                MessageBox.Show(Window.GetWindow(this)!, $"Failed to load lookups.\n\n{ex.Message}", "New Sale",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                ShowOverlay(false);
            }
        }

        // Reflectively attempt to read price columns without compile-time coupling
        private async Task<decimal?> TryResolvePriceAsync(int productId)
        {
            var sp = ((App)System.Windows.Application.Current).Services;
            using var scope = sp.CreateScope();
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

        // ---------- UI actions ----------
        private void BtnAddProduct_Click(object sender, RoutedEventArgs e)
        {
            var wnd = new BestFlex.Shell.Windows.QuickAddProductWindow
            {
                Owner = Window.GetWindow(this)
            };
            if (wnd.ShowDialog() == true)
            {
                // Refresh product list and preselect
                _ = LoadLookupsAsync().ContinueWith(_ =>
                {
                    Dispatcher.Invoke(() =>
                    {
                        var just = wnd.CreatedProduct;
                        var idProp = just?.GetType().GetProperty("Id");
                        if (idProp != null)
                        {
                            int v = (int)Convert.ChangeType(idProp.GetValue(just), typeof(int), CultureInfo.InvariantCulture);
                            var id = v;
                            var line = Lines.LastOrDefault() ?? AddLine();
                            line.ProductId = id;
                            line.UnitPrice = Products.FirstOrDefault(x => x.Id == id)?.DefaultPrice ?? 0m;
                            line.Qty = 1m;
                            UpdateTotals();
                        }
                    });
                });
            }
        }

        private void BtnAddCustomer_Click(object sender, RoutedEventArgs e)
        {
            var wnd = new BestFlex.Shell.Windows.QuickAddCustomerWindow
            {
                Owner = Window.GetWindow(this)
            };
            if (wnd.ShowDialog() == true)
            {
                // Prefill customer
                var created = wnd.CreatedCustomer;
                var nameProp = created?.GetType().GetProperty("Name");
                var name = nameProp?.GetValue(created)?.ToString();
                if (!string.IsNullOrWhiteSpace(name))
                {
                    // reload list and select
                    _ = LoadLookupsAsync().ContinueWith(_ =>
                    {
                        Dispatcher.Invoke(() =>
                        {
                            var list = (cmbCustomer.ItemsSource as System.Collections.IEnumerable)?.Cast<dynamic>().ToList();
                            var found = list?.FirstOrDefault(x => (string)x.Name == name);
                            if (found != null)
                                cmbCustomer.SelectedValue = (int)found.Id;
                        });
                    });
                }
            }
        }

        private LineVm AddLine()
        {
            var vm = new LineVm(this);
            Lines.Add(vm);
            return vm;
        }

        private void BtnAddLine_Click(object sender, RoutedEventArgs e) => AddLine();

        private void BtnRemoveLine_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button b && b.DataContext is LineVm vm)
            {
                Lines.Remove(vm);
                UpdateTotals();
            }
        }

        private void UpdateTotals()
        {
            var subtotal = Lines.Sum(l => l.LineTotal);
            txtSubTotal.Text = subtotal.ToString("N2", CultureInfo.InvariantCulture);
            txtItems.Text = Lines.Sum(l => l.Qty).ToString("N2", CultureInfo.InvariantCulture);
        }

        internal void OnLineChanged()
        {
            UpdateTotals();
        }

        private void ShowOverlay(bool on)
        {
            overlay.Visibility = on ? Visibility.Visible : Visibility.Collapsed;
            overlay.IsHitTestVisible = on;
        }

        // Save button is a placeholder to keep compatibility with your pipeline.
        // Wire this into your existing save handler if needed.
        private void BtnSave_Click(object sender, RoutedEventArgs e)
        {
            MessageBox.Show(Window.GetWindow(this)!,
                "UI is ready. Hook this page's Lines + selected Customer/Currency/Date into your existing save service.",
                "New Sale", MessageBoxButton.OK, MessageBoxImage.Information);
        }
    }

    // ---------- VMs ----------
    public sealed class ProductVm
    {
        public int Id { get; set; }
        public string Code { get; set; } = "";
        public string Name { get; set; } = "";
        public int StockQty { get; set; }
        public decimal DefaultPrice { get; set; }

        public string Display => string.IsNullOrWhiteSpace(Code) ? Name : $"{Code} — {Name}";
    }

    public sealed class LineVm : INotifyPropertyChanged
    {
        private readonly NewSalePage _owner;
        public event PropertyChangedEventHandler? PropertyChanged;
        private int _productId;
        private decimal _qty;
        private decimal _unitPrice;
        private int _stockQty;
        private string _productName = "";
        private string _productCode = "";

        public LineVm(NewSalePage owner)
        {
            _owner = owner;
        }

        public int ProductId
        {
            get => _productId;
            set
            {
                if (_productId == value) return;
                _productId = value;
                OnChanged();
                // auto-fill price and stock
                var p = _owner.Products.FirstOrDefault(x => x.Id == _productId);
                if (p != null)
                {
                    _productName = p.Name;
                    _productCode = p.Code;
                    StockQty = p.StockQty;
                    if (UnitPrice == 0m) UnitPrice = p.DefaultPrice;
                    if (Qty == 0m) Qty = 1m;
                }
                _owner.OnLineChanged();
                Raise(nameof(StockText));
            }
        }

        public string ProductName => _productName;
        public string ProductCode => _productCode;

        public int StockQty
        {
            get => _stockQty;
            private set { _stockQty = value; Raise(nameof(StockQty)); Raise(nameof(StockText)); }
        }

        public string StockText => StockQty <= 0 ? "⚠ out" : $"Stock: {StockQty:N0}";

        public decimal Qty
        {
            get => _qty;
            set { _qty = value < 0 ? 0 : value; OnChanged(); _owner.OnLineChanged(); }
        }

        public decimal UnitPrice
        {
            get => _unitPrice;
            set { _unitPrice = value < 0 ? 0 : value; OnChanged(); _owner.OnLineChanged(); }
        }

        public decimal LineTotal => Math.Round(Qty * UnitPrice, 2, MidpointRounding.AwayFromZero);

        private void OnChanged([CallerMemberName] string? p = null) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(p));
        private void Raise(string p) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(p));
    }
}
