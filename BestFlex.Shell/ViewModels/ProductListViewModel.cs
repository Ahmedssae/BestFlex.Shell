using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Input;
using BestFlex.Application.UI;

namespace BestFlex.Shell.ViewModels
{
    public class ProductListViewModel : INotifyPropertyChanged
    {
        private readonly IProductUiAdapter _productAdapter;
        private ObservableCollection<ProductListItemViewModel> _products = new();
        private string _searchTerm = string.Empty;
        private bool _includeInactive = false;
        private bool _isLoading = false;
        private string _errorMessage = string.Empty;
        private ProductListItemViewModel? _selectedProduct;
        private ICommand? _refreshCommand;
        private ICommand? _addProductCommand;
        private ICommand? _editProductCommand;
        private ICommand? _deactivateProductCommand;

        public ProductListViewModel(IProductUiAdapter productAdapter)
        {
            _productAdapter = productAdapter;
            InitializeCommands();
            // Async initialization should be called explicitly by the UI
        }

        public ObservableCollection<ProductListItemViewModel> Products
        {
            get => _products;
            set => SetProperty(ref _products, value, nameof(Products));
        }

        public string SearchTerm
        {
            get => _searchTerm;
            set
            {
                if (SetProperty(ref _searchTerm, value, nameof(SearchTerm)))
                {
                    FilterProducts();
                }
            }
        }

        public bool IncludeInactive
        {
            get => _includeInactive;
            set
            {
                if (SetProperty(ref _includeInactive, value, nameof(IncludeInactive)))
                {
                    FilterProducts();
                }
            }
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

        public ProductListItemViewModel? SelectedProduct
        {
            get => _selectedProduct;
            set => SetProperty(ref _selectedProduct, value, nameof(SelectedProduct));
        }

        public ICommand RefreshCommand => _refreshCommand ??= new BestFlex.Shell.Infrastructure.RelayCommand(async () => await LoadProductsAsync());
        public ICommand AddProductCommand => _addProductCommand ??= new BestFlex.Shell.Infrastructure.RelayCommand(async () => await AddProduct());
        public ICommand EditProductCommand => _editProductCommand ??= new BestFlex.Shell.Infrastructure.RelayCommand(async () => await EditProduct(), () => SelectedProduct != null);
        public ICommand DeactivateProductCommand => _deactivateProductCommand ??= new BestFlex.Shell.Infrastructure.RelayCommand(DeactivateProduct, () => SelectedProduct != null && SelectedProduct.IsActive);

        private void InitializeCommands()
        {
            _refreshCommand = new BestFlex.Shell.Infrastructure.RelayCommand(async () => await LoadProductsAsync());
            _addProductCommand = new BestFlex.Shell.Infrastructure.RelayCommand(async () => await AddProduct());
            _editProductCommand = new BestFlex.Shell.Infrastructure.RelayCommand(async () => await EditProduct(), () => SelectedProduct != null);
            _deactivateProductCommand = new BestFlex.Shell.Infrastructure.RelayCommand(DeactivateProduct, () => SelectedProduct != null && SelectedProduct.IsActive);
        }

        public async Task LoadProductsAsync()
        {
            try
            {
                IsLoading = true;
                ErrorMessage = string.Empty;

                var result = await _productAdapter.GetProductsAsync();
                if (result.Success)
                {
                    Products.Clear();
                    foreach (var product in result.Products)
                    {
                        Products.Add(new ProductListItemViewModel(product));
                    }
                    FilterProducts();
                }
                else
                {
                    ErrorMessage = result.UserFriendlyMessage;
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

        private void FilterProducts()
        {
            try
            {
                var filtered = Products.AsEnumerable();

                if (!string.IsNullOrWhiteSpace(SearchTerm))
                {
                    filtered = filtered.Where(p => 
                        p.Name.Contains(SearchTerm, StringComparison.OrdinalIgnoreCase) ||
                        p.Sku.Contains(SearchTerm, StringComparison.OrdinalIgnoreCase));
                }

                if (!IncludeInactive)
                {
                    filtered = filtered.Where(p => p.IsActive);
                }

                // Update the collection without clearing and recreating
                var filteredList = filtered.ToList();
                var toRemove = Products.Except(filteredList).ToList();
                var toAdd = filteredList.Except(Products).ToList();

                foreach (var item in toRemove)
                {
                    Products.Remove(item);
                }

                foreach (var item in toAdd)
                {
                    Products.Add(item);
                }
            }
            catch (Exception ex)
            {
                ErrorMessage = "Filter error: " + ex.Message;
            }
        }

        private async Task AddProduct()
        {
            try
            {
                // TODO: Create ProductEditWindow
                // var window = new BestFlex.Shell.Views.ProductEditWindow();
                // window.Owner = System.Windows.Application.Current.MainWindow;
                // window.ShowDialog();
                await LoadProductsAsync(); // Refresh after adding
            }
            catch (Exception ex)
            {
                ErrorMessage = "Failed to open add product window: " + ex.Message;
            }
        }

        private async Task EditProduct()
        {
            try
            {
                if (SelectedProduct == null) return;

                // TODO: Create ProductEditWindow
                // var window = new BestFlex.Shell.Views.ProductEditWindow(SelectedProduct.Id);
                // window.Owner = System.Windows.Application.Current.MainWindow;
                // window.ShowDialog();
                await LoadProductsAsync(); // Refresh after editing
            }
            catch (Exception ex)
            {
                ErrorMessage = "Failed to open edit product window: " + ex.Message;
            }
        }

        private async void DeactivateProduct()
        {
            try
            {
                if (SelectedProduct == null) return;

                var result = await _productAdapter.DeactivateProductAsync(new DeactivateProductUiRequest
                {
                    ProductId = SelectedProduct.Id,
                    Reason = "Deactivated from product list"
                });

                if (result.Success)
                {
                    await LoadProductsAsync(); // Refresh list
                }
                else
                {
                    ErrorMessage = result.UserFriendlyMessage;
                    if (result.ValidationErrors.Any())
                    {
                        ErrorMessage += " " + string.Join(", ", result.ValidationErrors.Select(e => e.ErrorMessage));
                    }
                }
            }
            catch (Exception ex)
            {
                ErrorMessage = "Failed to deactivate product: " + ex.Message;
            }
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

    public class ProductListItemViewModel : INotifyPropertyChanged
    {
        private readonly ProductUiDto _dto;

        public ProductListItemViewModel(ProductUiDto dto)
        {
            _dto = dto;
        }

        public int Id => _dto.Id;
        public string Name => _dto.Name;
        public string Sku => _dto.SKU;
        public decimal Cost => _dto.Cost;
        public decimal BasePrice => _dto.BasePrice;
        public bool IsActive => _dto.IsActive;
        public string Status => IsActive ? "Active" : "Inactive";
        public DateTime CreatedAt => _dto.CreatedAt;

        public event PropertyChangedEventHandler? PropertyChanged;

        protected virtual void OnPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}
