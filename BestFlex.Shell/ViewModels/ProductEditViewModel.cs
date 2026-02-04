using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Input;
using BestFlex.Application.UI;

namespace BestFlex.Shell.ViewModels
{
    public class ProductEditViewModel : INotifyPropertyChanged
    {
        private readonly IProductUiAdapter _productAdapter;
        private readonly int? _productId;
        
        private string _sku = string.Empty;
        private string _name = string.Empty;
        private string _description = string.Empty;
        private decimal _cost;
        private decimal _basePrice;
        private bool _isLoading = false;
        private string _errorMessage = string.Empty;
        private ObservableCollection<ProductValidationError> _validationErrors = new();
        private ObservableCollection<PriceTierViewModel> _priceTiers = new();
        private ICommand? _saveCommand;
        private ICommand? _cancelCommand;
        private ICommand? _addPriceTierCommand;
        private ICommand? _removePriceTierCommand;

        public ProductEditViewModel(IProductUiAdapter productAdapter)
        {
            _productAdapter = productAdapter;
            InitializeCommands();
        }

        public ProductEditViewModel(IProductUiAdapter productAdapter, int productId)
        {
            _productAdapter = productAdapter;
            _productId = productId;
            InitializeCommands();
            // Async initialization should be called explicitly by the UI
        }

        public string Title => _productId.HasValue ? "Edit Product" : "Add Product";

        public string SKU
        {
            get => _sku;
            set => SetProperty(ref _sku, value, nameof(SKU));
        }

        public string Name
        {
            get => _name;
            set => SetProperty(ref _name, value, nameof(Name));
        }

        public string Description
        {
            get => _description;
            set => SetProperty(ref _description, value, nameof(Description));
        }

        public decimal Cost
        {
            get => _cost;
            set => SetProperty(ref _cost, value, nameof(Cost));
        }

        public decimal BasePrice
        {
            get => _basePrice;
            set => SetProperty(ref _basePrice, value, nameof(BasePrice));
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

        public ObservableCollection<ProductValidationError> ValidationErrors
        {
            get => _validationErrors;
            set => SetProperty(ref _validationErrors, value, nameof(ValidationErrors));
        }

        public ObservableCollection<PriceTierViewModel> PriceTiers
        {
            get => _priceTiers;
            set => SetProperty(ref _priceTiers, value, nameof(PriceTiers));
        }

        public ICommand SaveCommand => _saveCommand ??= new BestFlex.Shell.Infrastructure.RelayCommand(async () => await SaveAsync());
        public ICommand CancelCommand => _cancelCommand ??= new BestFlex.Shell.Infrastructure.RelayCommand(Cancel);
        public ICommand AddPriceTierCommand => _addPriceTierCommand ??= new BestFlex.Shell.Infrastructure.RelayCommand(AddPriceTier);
        public ICommand RemovePriceTierCommand => _removePriceTierCommand ??= new BestFlex.Shell.Infrastructure.RelayCommand((object? param) => { if (param is PriceTierViewModel tier) RemovePriceTier(tier); });

        private void InitializeCommands()
        {
            _saveCommand = new BestFlex.Shell.Infrastructure.RelayCommand(async () => await SaveAsync());
            _cancelCommand = new BestFlex.Shell.Infrastructure.RelayCommand(Cancel);
            _addPriceTierCommand = new BestFlex.Shell.Infrastructure.RelayCommand(AddPriceTier);
            _removePriceTierCommand = new BestFlex.Shell.Infrastructure.RelayCommand((object? param) => { if (param is PriceTierViewModel tier) RemovePriceTier(tier); });
        }

        public async Task LoadProductAsync()
        {
            if (!_productId.HasValue) return;

            try
            {
                IsLoading = true;
                ErrorMessage = string.Empty;

                var result = await _productAdapter.GetProductsAsync();
                if (result.Success)
                {
                    var product = result.Products.FirstOrDefault(p => p.Id == _productId.Value);
                    if (product != null)
                    {
                        SKU = product.SKU;
                        Name = product.Name;
                        Description = product.Description;
                        Cost = product.Cost;
                        BasePrice = product.BasePrice;

                        PriceTiers.Clear();
                        foreach (var tier in product.PriceTiers)
                        {
                            PriceTiers.Add(new PriceTierViewModel(tier));
                        }
                    }
                    else
                    {
                        ErrorMessage = "Product not found";
                    }
                }
                else
                {
                    ErrorMessage = result.UserFriendlyMessage;
                }
            }
            catch (Exception ex)
            {
                ErrorMessage = "Failed to load product: " + ex.Message;
            }
            finally
            {
                IsLoading = false;
            }
        }

        public async Task SaveAsync()
        {
            try
            {
                IsLoading = true;
                ErrorMessage = string.Empty;
                ValidationErrors.Clear();

                if (_productId.HasValue)
                {
                    // Update existing product
                    var updateRequest = new UpdateProductUiRequest
                    {
                        Id = _productId.Value,
                        Description = Description,
                        Cost = Cost,
                        Price = BasePrice
                    };

                    var result = await _productAdapter.UpdateProductAsync(updateRequest);
                    if (!result.Success)
                    {
                        ErrorMessage = result.UserFriendlyMessage;
                        ValidationErrors = new ObservableCollection<ProductValidationError>(result.ValidationErrors);
                        return;
                    }
                }
                else
                {
                    // Create new product
                    var createRequest = new CreateProductUiRequest
                    {
                        SKU = SKU,
                        Description = Description,
                        Cost = Cost,
                        Price = BasePrice
                    };

                    var result = await _productAdapter.CreateProductAsync(createRequest);
                    if (!result.Success)
                    {
                        ErrorMessage = result.UserFriendlyMessage;
                        ValidationErrors = new ObservableCollection<ProductValidationError>(result.ValidationErrors);
                        return;
                    }
                }

                // Save price tiers if we have a product ID
                var productId = _productId ?? 0; // This would be set from creation result in real implementation
                foreach (var tier in PriceTiers.Where(t => t.IsDirty))
                {
                    var tierRequest = new AddPriceTierUiRequest
                    {
                        ProductId = productId,
                        QuantityFrom = tier.QuantityFrom,
                        QuantityTo = tier.QuantityTo,
                        Price = tier.Price,
                        Currency = tier.Currency
                    };

                    var tierResult = await _productAdapter.AddPriceTierAsync(tierRequest);
                    if (!tierResult.Success)
                    {
                        ErrorMessage = $"Failed to save price tier: {tierResult.UserFriendlyMessage}";
                        return;
                    }
                }

                // Close window on success
                System.Windows.Application.Current.Windows.OfType<System.Windows.Window>()
                    .FirstOrDefault(w => w.DataContext == this)?.Close();
            }
            catch (Exception ex)
            {
                ErrorMessage = "Failed to save product: " + ex.Message;
            }
            finally
            {
                IsLoading = false;
            }
        }

        private void Cancel()
        {
            System.Windows.Application.Current.Windows.OfType<System.Windows.Window>()
                .FirstOrDefault(w => w.DataContext == this)?.Close();
        }

        private void AddPriceTier()
        {
            var newTier = new PriceTierViewModel
            {
                QuantityFrom = PriceTiers.Any() ? PriceTiers.Max(t => t.QuantityTo) + 1 : 1,
                QuantityTo = PriceTiers.Any() ? PriceTiers.Max(t => t.QuantityTo) + 10 : 10,
                Price = BasePrice * 0.9m, // 10% discount by default
                Currency = "USD"
            };
            PriceTiers.Add(newTier);
        }

        private void RemovePriceTier(PriceTierViewModel tier)
        {
            if (tier != null)
            {
                PriceTiers.Remove(tier);
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

    public class PriceTierViewModel : INotifyPropertyChanged
    {
        private readonly PriceTierDto? _dto;
        private decimal _quantityFrom;
        private decimal _quantityTo;
        private decimal _price;
        private string _currency = "USD";
        private bool _isDirty = false;

        public PriceTierViewModel()
        {
            _isDirty = true; // New tiers are dirty by default
        }

        public PriceTierViewModel(PriceTierDto dto)
        {
            _dto = dto;
            _quantityFrom = dto.QuantityFrom;
            _quantityTo = dto.QuantityTo;
            _price = dto.Price;
            _currency = dto.Currency;
        }

        public decimal QuantityFrom
        {
            get => _quantityFrom;
            set
            {
                if (SetProperty(ref _quantityFrom, value, nameof(QuantityFrom)))
                {
                    _isDirty = true;
                }
            }
        }

        public decimal QuantityTo
        {
            get => _quantityTo;
            set
            {
                if (SetProperty(ref _quantityTo, value, nameof(QuantityTo)))
                {
                    _isDirty = true;
                }
            }
        }

        public decimal Price
        {
            get => _price;
            set
            {
                if (SetProperty(ref _price, value, nameof(Price)))
                {
                    _isDirty = true;
                }
            }
        }

        public string Currency
        {
            get => _currency;
            set
            {
                if (SetProperty(ref _currency, value, nameof(Currency)))
                {
                    _isDirty = true;
                }
            }
        }

        public bool IsDirty => _isDirty;

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
