using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Input;
using BestFlex.Application.UI;

namespace BestFlex.Shell.ViewModels
{
    public class InventoryOverviewViewModel : INotifyPropertyChanged
    {
        private readonly IInventoryUiAdapter _inventoryAdapter;
        private ObservableCollection<InventoryItemViewModel> _inventoryItems = new();
        private string _searchTerm = string.Empty;
        private bool _includeInactive = false;
        private bool _isLoading = false;
        private string _errorMessage = string.Empty;
        private InventoryItemViewModel? _selectedItem;
        private ICommand? _refreshCommand;
        private ICommand? _viewDetailsCommand;
        
        // Summary properties
        private int _totalProducts = 0;
        private decimal _totalStockValue = 0;
        private decimal _totalReservedStock = 0;
        private decimal _totalAvailableStock = 0;
        private string _valuationMethod = "FIFO";

        public InventoryOverviewViewModel(IInventoryUiAdapter inventoryAdapter)
        {
            _inventoryAdapter = inventoryAdapter;
            InitializeCommands();
            // Async initialization should be called explicitly by the UI
        }

        public ObservableCollection<InventoryItemViewModel> InventoryItems
        {
            get => _inventoryItems;
            set => SetProperty(ref _inventoryItems, value, nameof(InventoryItems));
        }

        public string SearchTerm
        {
            get => _searchTerm;
            set
            {
                if (SetProperty(ref _searchTerm, value, nameof(SearchTerm)))
                {
                    FilterInventory();
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
                    FilterInventory();
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

        public InventoryItemViewModel? SelectedItem
        {
            get => _selectedItem;
            set => SetProperty(ref _selectedItem, value, nameof(SelectedItem));
        }

        // Summary Properties
        public int TotalProducts
        {
            get => _totalProducts;
            set => SetProperty(ref _totalProducts, value, nameof(TotalProducts));
        }

        public decimal TotalStockValue
        {
            get => _totalStockValue;
            set => SetProperty(ref _totalStockValue, value, nameof(TotalStockValue));
        }

        public decimal TotalReservedStock
        {
            get => _totalReservedStock;
            set => SetProperty(ref _totalReservedStock, value, nameof(TotalReservedStock));
        }

        public decimal TotalAvailableStock
        {
            get => _totalAvailableStock;
            set => SetProperty(ref _totalAvailableStock, value, nameof(TotalAvailableStock));
        }

        public string ValuationMethod
        {
            get => _valuationMethod;
            set => SetProperty(ref _valuationMethod, value, nameof(ValuationMethod));
        }

        public ICommand RefreshCommand => _refreshCommand ??= new BestFlex.Shell.Infrastructure.RelayCommand(async () => await LoadInventoryAsync());
        public ICommand ViewDetailsCommand => _viewDetailsCommand ??= new BestFlex.Shell.Infrastructure.RelayCommand(ViewDetails, () => SelectedItem != null);

        private void InitializeCommands()
        {
            _refreshCommand = new BestFlex.Shell.Infrastructure.RelayCommand(async () => await LoadInventoryAsync());
            _viewDetailsCommand = new BestFlex.Shell.Infrastructure.RelayCommand(ViewDetails, () => SelectedItem != null);
        }

        public async Task LoadInventoryAsync()
        {
            try
            {
                IsLoading = true;
                ErrorMessage = string.Empty;

                var result = await _inventoryAdapter.GetInventoryOverviewAsync();
                if (result.Success)
                {
                    InventoryItems.Clear();
                    foreach (var item in result.InventoryItems)
                    {
                        InventoryItems.Add(new InventoryItemViewModel(item));
                    }
                    
                    UpdateSummary();
                    FilterInventory();
                }
                else
                {
                    ErrorMessage = result.UserFriendlyMessage;
                }
            }
            catch (Exception ex)
            {
                ErrorMessage = "Failed to load inventory: " + ex.Message;
            }
            finally
            {
                IsLoading = false;
            }
        }

        private void FilterInventory()
        {
            try
            {
                var filtered = InventoryItems.AsEnumerable();

                if (!string.IsNullOrWhiteSpace(SearchTerm))
                {
                    filtered = filtered.Where(item => 
                        item.ProductName.Contains(SearchTerm, StringComparison.OrdinalIgnoreCase) ||
                        item.SKU.Contains(SearchTerm, StringComparison.OrdinalIgnoreCase));
                }

                if (!IncludeInactive)
                {
                    filtered = filtered.Where(item => item.IsActive);
                }

                // Update the collection without clearing and recreating
                var filteredList = filtered.ToList();
                var toRemove = InventoryItems.Except(filteredList).ToList();
                var toAdd = filteredList.Except(InventoryItems).ToList();

                foreach (var item in toRemove)
                {
                    InventoryItems.Remove(item);
                }

                foreach (var item in toAdd)
                {
                    InventoryItems.Add(item);
                }
            }
            catch (Exception ex)
            {
                ErrorMessage = "Filter error: " + ex.Message;
            }
        }

        private void UpdateSummary()
        {
            TotalProducts = InventoryItems.Count;
            TotalStockValue = InventoryItems.Sum(item => item.TotalValue);
            TotalReservedStock = InventoryItems.Sum(item => item.ReservedQuantity);
            TotalAvailableStock = InventoryItems.Sum(item => item.AvailableQuantity);
        }

        private void ViewDetails()
        {
            try
            {
                if (SelectedItem == null) return;

                // TODO: Create inventory details window
                // var window = new BestFlex.Shell.Views.InventoryDetailsWindow(SelectedItem.ProductId);
                // window.Owner = System.Windows.Application.Current.MainWindow;
                // window.ShowDialog();
            }
            catch (Exception ex)
            {
                ErrorMessage = "Failed to open inventory details: " + ex.Message;
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

    public class InventoryItemViewModel : INotifyPropertyChanged
    {
        private readonly InventoryOverviewItemDto _dto;

        public InventoryItemViewModel(InventoryOverviewItemDto dto)
        {
            _dto = dto;
        }

        public int ProductId => _dto.ProductId;
        public string SKU => _dto.SKU;
        public string ProductName => _dto.ProductName;
        public decimal TotalQuantity => _dto.TotalQuantity;
        public decimal AvailableQuantity => _dto.AvailableQuantity;
        public decimal ReservedQuantity => _dto.ReservedQuantity;
        public decimal UnitCost => _dto.UnitCost;
        public decimal TotalValue => _dto.TotalValue;
        public string ValuationMethod => _dto.ValuationMethod;
        public bool IsActive => _dto.IsActive;
        public string Status => IsActive ? "Active" : "Inactive";
        public DateTime LastUpdated => _dto.LastUpdated;
        public string LastUpdatedFormatted => LastUpdated.ToString("yyyy-MM-dd HH:mm");

        // Computed properties for UI
        public decimal UtilizationPercentage => TotalQuantity > 0 ? (ReservedQuantity / TotalQuantity) * 100 : 0;
        public string UtilizationStatus => UtilizationPercentage switch
        {
            >= 80 => "High",
            >= 50 => "Medium",
            _ => "Low"
        };

        public event PropertyChangedEventHandler? PropertyChanged;

        protected virtual void OnPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}
