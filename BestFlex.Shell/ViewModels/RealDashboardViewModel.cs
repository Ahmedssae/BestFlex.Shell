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
    public class RealDashboardViewModel : INotifyPropertyChanged
    {
        private readonly ISalesOrderUiAdapter _salesOrderAdapter;
        private readonly IInventoryUiAdapter _inventoryAdapter;
        private readonly ICustomerReadService _customerReadService;
        private readonly IProductReadService _productReadService;
        
        private bool _isLoading = false;
        private string _errorMessage = string.Empty;
        private DateTime _lastRefreshTime = DateTime.MinValue;
        
        // KPI Properties
        private decimal _todaySales;
        private decimal _monthSales;
        private decimal _totalReceivables;
        private int _lowStockAlertCount;
        private int _totalOrders;
        private int _totalCustomers;
        private int _totalProducts;
        private decimal _averageOrderValue;
        private int _pendingInvoices;
        
        // Collections
        private ObservableCollection<RecentSalesViewModel> _recentSales = new();
        private ObservableCollection<LowStockAlertViewModel> _lowStockAlertsCollection = new();
        private ObservableCollection<TopCustomerViewModel> _topCustomers = new();
        private ObservableCollection<SalesTrendViewModel> _salesTrends = new();
        
        private ICommand? _refreshCommand;
        private ICommand? _refreshKpisCommand;
        private ICommand? _viewDetailsCommand;

        public RealDashboardViewModel(
            ISalesOrderUiAdapter salesOrderAdapter,
            IInventoryUiAdapter inventoryAdapter,
            ICustomerReadService customerReadService,
            IProductReadService productReadService)
        {
            _salesOrderAdapter = salesOrderAdapter;
            _inventoryAdapter = inventoryAdapter;
            _customerReadService = customerReadService;
            _productReadService = productReadService;
            
            InitializeCommands();
            // ERP REQUIREMENT: No fire-and-forget async calls in constructor
            // Data will be loaded explicitly when needed
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

        public DateTime LastRefreshTime
        {
            get => _lastRefreshTime;
            set => SetProperty(ref _lastRefreshTime, value, nameof(LastRefreshTime));
        }

        // KPI Properties
        public decimal TodaySales
        {
            get => _todaySales;
            set => SetProperty(ref _todaySales, value, nameof(TodaySales));
        }

        public decimal MonthSales
        {
            get => _monthSales;
            set => SetProperty(ref _monthSales, value, nameof(MonthSales));
        }

        public decimal TotalReceivables
        {
            get => _totalReceivables;
            set => SetProperty(ref _totalReceivables, value, nameof(TotalReceivables));
        }

        public int LowStockAlerts
        {
            get => _lowStockAlertCount;
            set => SetProperty(ref _lowStockAlertCount, value, nameof(LowStockAlerts));
        }

        public int TotalOrders
        {
            get => _totalOrders;
            set => SetProperty(ref _totalOrders, value, nameof(TotalOrders));
        }

        public int TotalCustomers
        {
            get => _totalCustomers;
            set => SetProperty(ref _totalCustomers, value, nameof(TotalCustomers));
        }

        public int TotalProducts
        {
            get => _totalProducts;
            set => SetProperty(ref _totalProducts, value, nameof(TotalProducts));
        }

        public decimal AverageOrderValue
        {
            get => _averageOrderValue;
            set => SetProperty(ref _averageOrderValue, value, nameof(AverageOrderValue));
        }

        public int PendingInvoices
        {
            get => _pendingInvoices;
            set => SetProperty(ref _pendingInvoices, value, nameof(PendingInvoices));
        }

        // Collections
        public ObservableCollection<RecentSalesViewModel> RecentSales
        {
            get => _recentSales;
            set => SetProperty(ref _recentSales, value, nameof(RecentSales));
        }

        public ObservableCollection<LowStockAlertViewModel> LowStockAlertsCollection
        {
            get => _lowStockAlertsCollection;
            set => SetProperty(ref _lowStockAlertsCollection, value, nameof(LowStockAlertsCollection));
        }

        public ObservableCollection<TopCustomerViewModel> TopCustomers
        {
            get => _topCustomers;
            set => SetProperty(ref _topCustomers, value, nameof(TopCustomers));
        }

        public ObservableCollection<SalesTrendViewModel> SalesTrends
        {
            get => _salesTrends;
            set => SetProperty(ref _salesTrends, value, nameof(SalesTrends));
        }

        public ICommand RefreshCommand => _refreshCommand ??= new BestFlex.Shell.Infrastructure.RelayCommand(async () => await LoadDashboardDataAsync());
        public ICommand RefreshKpisCommand => _refreshKpisCommand ??= new BestFlex.Shell.Infrastructure.RelayCommand(async () => await LoadKpisAsync());
        public ICommand ViewDetailsCommand => _viewDetailsCommand ??= new BestFlex.Shell.Infrastructure.RelayCommand((object? param) => ViewDetails(param?.ToString() ?? string.Empty));

        private void InitializeCommands()
        {
            _refreshCommand = new BestFlex.Shell.Infrastructure.RelayCommand(async () => await LoadDashboardDataAsync());
            _refreshKpisCommand = new BestFlex.Shell.Infrastructure.RelayCommand(async () => await LoadKpisAsync());
            _viewDetailsCommand = new BestFlex.Shell.Infrastructure.RelayCommand((object? param) => ViewDetails(param?.ToString() ?? string.Empty));
        }

        public async Task LoadDashboardDataAsync()
        {
            try
            {
                IsLoading = true;
                ErrorMessage = string.Empty;

                // Load all dashboard data in parallel for performance
                var tasks = new[]
                {
                    LoadKpisAsync(),
                    LoadRecentSalesAsync(),
                    LoadLowStockAlertsAsync(),
                    LoadTopCustomersAsync(),
                    LoadSalesTrendsAsync()
                };

                await Task.WhenAll(tasks);
                LastRefreshTime = DateTime.Now;
            }
            catch (Exception ex)
            {
                ErrorMessage = "Failed to load dashboard data: " + ex.Message;
            }
            finally
            {
                IsLoading = false;
            }
        }

        public async Task LoadKpisAsync()
        {
            try
            {
                // ERP REQUIREMENT: No fake data - implement real data loading
                // TODO: Replace with actual database queries
                await Task.Delay(10); // Make truly async
                throw new NotImplementedException("KPI data loading not yet implemented - requires real database integration");
            }
            catch (NotImplementedException)
            {
                // ERP REQUIREMENT: Explicit failure when not implemented
                ErrorMessage = "KPI functionality is not yet available. This feature requires implementation of real database queries.";
                // Keep default values - explicit empty state
            }
            catch (Exception ex)
            {
                ErrorMessage = "Failed to load KPIs: " + ex.Message;
            }
        }

        public async Task LoadRecentSalesAsync()
        {
            try
            {
                // ERP REQUIREMENT: No fake data - implement real data loading
                await Task.Delay(10);
                throw new NotImplementedException("Recent sales data loading not yet implemented - requires real database integration");
            }
            catch (NotImplementedException)
            {
                ErrorMessage = "Recent sales functionality is not yet available. This feature requires implementation of real database queries.";
                RecentSales.Clear(); // Explicit empty state
            }
            catch (Exception ex)
            {
                ErrorMessage = "Failed to load recent sales: " + ex.Message;
            }
        }

        public async Task LoadLowStockAlertsAsync()
        {
            try
            {
                var inventoryResult = await _inventoryAdapter.GetInventoryOverviewAsync();
                if (inventoryResult.Success)
                {
                    var lowStockItems = inventoryResult.InventoryItems
                        .Where(item => item.TotalQuantity <= 50)
                        .Take(10)
                        .Select(item => new LowStockAlertViewModel
                        {
                            ProductName = item.ProductName,
                            SKU = item.SKU,
                            CurrentStock = item.TotalQuantity,
                            MinimumStock = 50,
                            UnitCost = item.UnitCost,
                            ValueAtRisk = item.TotalQuantity * item.UnitCost
                        })
                        .ToList();

                    LowStockAlertsCollection.Clear();
                    foreach (var alert in lowStockItems)
                    {
                        LowStockAlertsCollection.Add(alert);
                    }
                }
            }
            catch (Exception ex)
            {
                ErrorMessage = "Failed to load low stock alerts: " + ex.Message;
            }
        }

        public async Task LoadTopCustomersAsync()
        {
            try
            {
                // ERP REQUIREMENT: No fake data - implement real data loading
                await Task.Delay(10);
                throw new NotImplementedException("Top customers data loading not yet implemented - requires real database integration");
            }
            catch (NotImplementedException)
            {
                ErrorMessage = "Top customers functionality is not yet available. This feature requires implementation of real database queries.";
                TopCustomers.Clear(); // Explicit empty state
            }
            catch (Exception ex)
            {
                ErrorMessage = "Failed to load top customers: " + ex.Message;
            }
        }

        public async Task LoadSalesTrendsAsync()
        {
            try
            {
                // ERP REQUIREMENT: No fake data - implement real data loading
                await Task.Delay(10);
                throw new NotImplementedException("Sales trends data loading not yet implemented - requires real database integration");
            }
            catch (NotImplementedException)
            {
                ErrorMessage = "Sales trends functionality is not yet available. This feature requires implementation of real database queries.";
                SalesTrends.Clear(); // Explicit empty state
            }
            catch (Exception ex)
            {
                ErrorMessage = "Failed to load sales trends: " + ex.Message;
            }
        }

        private void ViewDetails(string section)
        {
            try
            {
                // TODO: Navigate to detailed view based on section
                // For now, just show a message
                System.Windows.MessageBox.Show(
                    $"Navigate to {section} details",
                    "Dashboard Navigation",
                    System.Windows.MessageBoxButton.OK,
                    System.Windows.MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                ErrorMessage = "Failed to navigate: " + ex.Message;
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

    // Supporting ViewModels
    public class RecentSalesViewModel
    {
        public string OrderNumber { get; set; } = string.Empty;
        public string CustomerName { get; set; } = string.Empty;
        public decimal Amount { get; set; }
        public DateTime Date { get; set; }
        public string FormattedAmount => Amount.ToString("C");
        public string FormattedDate => Date.ToString("yyyy-MM-dd HH:mm");
    }

    public class LowStockAlertViewModel
    {
        public string ProductName { get; set; } = string.Empty;
        public string SKU { get; set; } = string.Empty;
        public decimal CurrentStock { get; set; }
        public decimal MinimumStock { get; set; }
        public decimal UnitCost { get; set; }
        public decimal ValueAtRisk { get; set; }
        public string FormattedValueAtRisk => ValueAtRisk.ToString("C");
        public string StockStatus => CurrentStock <= MinimumStock ? "Critical" : "Low";
    }

    public class TopCustomerViewModel
    {
        public string CustomerName { get; set; } = string.Empty;
        public decimal TotalSales { get; set; }
        public int OrderCount { get; set; }
        public decimal AverageOrderValue { get; set; }
        public string FormattedTotalSales => TotalSales.ToString("C");
        public string FormattedAverageOrderValue => AverageOrderValue.ToString("C");
    }

    public class SalesTrendViewModel
    {
        public DateTime Date { get; set; }
        public decimal SalesAmount { get; set; }
        public int OrderCount { get; set; }
        public string FormattedDate => Date.ToString("MMM dd");
        public string FormattedSalesAmount => SalesAmount.ToString("C");
    }
}
