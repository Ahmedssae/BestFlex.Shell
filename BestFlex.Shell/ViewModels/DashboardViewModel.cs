using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Input;
using BestFlex.Application.Abstractions;
using BestFlex.Infrastructure.Services;
using BestFlex.Persistence.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace BestFlex.Shell.ViewModels
{
    public sealed class ChartPointVm
    {
        public object Label { get; set; } = null!;
        public decimal Total { get; set; }
    }

    public sealed class DashboardViewModel : ViewModelBase
    {
        private readonly IServiceProvider _sp;
        private readonly IPermissionService _permissions;
        private readonly IAuditService _audit;
        private readonly IErrorService _error;
        private readonly BestFlex.Application.Abstractions.INavigationService _nav;
        private readonly Infrastructure.PaginationState _customerPaging = new();
        private readonly Infrastructure.PaginationState _lowStockPaging = new();
        private readonly Infrastructure.PaginationState _debtPaging = new();
        private readonly AsyncRelayCommand _refreshCmd;
        private readonly AsyncRelayCommand _refreshLowStockCmd;
        private readonly AsyncRelayCommand _refreshDebtCmd;
        private readonly AsyncRelayCommand _loadAllCmd;
        private readonly AsyncRelayCommand _openLowStockCmd;
        private readonly AsyncRelayCommand _openDebtCmd;
        private readonly SemaphoreSlim _loadLock = new(1, 1);
        
        private int _currentThreshold = 10;
        private int _currentLowTopN = 10;
        private int _currentDebtTopN = 10;
        private DateTime _currentFrom = DateTime.Today.AddDays(-30);
        private DateTime _currentTo = DateTime.Today;

        public DashboardViewModel(IServiceProvider sp, BestFlex.Application.Abstractions.INavigationService nav)
        {
            _sp = sp ?? throw new ArgumentNullException(nameof(sp));
            _nav = nav ?? throw new ArgumentNullException(nameof(nav));
            _permissions = sp.GetRequiredService<IPermissionService>();
            _audit = sp.GetRequiredService<IAuditService>();
            _error = sp.GetRequiredService<IErrorService>();
            
            _refreshCmd = new AsyncRelayCommand(RefreshAsync, () => !IsBusy && CanViewDashboard);
            _refreshLowStockCmd = new AsyncRelayCommand(RefreshLowStockAsync, () => !IsBusy && CanViewLowStock);
            _refreshDebtCmd = new AsyncRelayCommand(RefreshDebtAsync, () => !IsBusy && CanViewDebt);
            _loadAllCmd = new AsyncRelayCommand(LoadAllAsync, () => !IsBusy && CanViewDashboard);
            _openLowStockCmd = new AsyncRelayCommand(OpenLowStock, () => !IsBusy && CanViewLowStock);
            _openDebtCmd = new AsyncRelayCommand(OpenDebt, () => !IsBusy && CanViewDebt);
        }

        private bool _isBusy;
        public bool IsBusy { get => _isBusy; private set => SetProperty(ref _isBusy, value); }

        private decimal _todaySales;
        public decimal TodaySales { get => _todaySales; private set => SetProperty(ref _todaySales, value); }

        private decimal _monthSales;
        public decimal MonthSales { get => _monthSales; private set => SetProperty(ref _monthSales, value); }

        private decimal _totalReceivables;
        public decimal TotalReceivables { get => _totalReceivables; private set => SetProperty(ref _totalReceivables, value); }

        // Permission properties
        public bool CanViewDashboard => _permissions.CanViewDashboard();
        public bool CanViewLowStock => _permissions.CanViewLowStock();
        public bool CanViewDebt => _permissions.CanViewDebt();
        
        // Commands
        public ICommand RefreshCommand => _refreshCmd;
        public ICommand RefreshLowStockCommand => _refreshLowStockCmd;
        public ICommand RefreshDebtCommand => _refreshDebtCmd;
        public ICommand LoadAllCommand => _loadAllCmd;
        public ICommand OpenLowStockCommand => _openLowStockCmd;
        public ICommand OpenDebtCommand => _openDebtCmd;
        
        public ObservableCollection<ChartPointVm> SalesByDay { get; } = new();
        public ObservableCollection<ChartPointVm> SalesByCustomer { get; } = new();
        public int CustomerPageIndex { get => _customerPaging.PageIndex; set { _customerPaging.Update(Math.Max(1,value), _customerPaging.PageSize, _customerPaging.TotalCount); OnPropertyChanged(nameof(CustomerPageIndex)); OnPropertyChanged(nameof(CustomerPageIndicatorText)); } }
        public int CustomerPageSize { get => _customerPaging.PageSize; set { _customerPaging.Update(1, Math.Max(1,value), _customerPaging.TotalCount); OnPropertyChanged(nameof(CustomerPageSize)); } }
        
        public int CustomerTotalPages => Math.Max(1, (int)Math.Ceiling(_customerPaging.TotalCount / (double)CustomerPageSize));
        public string CustomerPageIndicatorText => $"Page {CustomerPageIndex} of {CustomerTotalPages}";

        // Keep low stock / debt collections for the dashboard UI
        public ObservableCollection<InventoryReadService.LowStockDto> LowStock { get; } = new();
        public ObservableCollection<SalesReadService.CustomerOutstandingDto> TopDebt { get; } = new();

        public string LowSummary { get; private set; } = string.Empty;
        public string DebtSummary { get; private set; } = string.Empty;

        public async Task LoadAsync(DateTime from, DateTime to, CancellationToken ct = default)
        {
            if (IsBusy) return;
            
            await _loadLock.WaitAsync(ct);
            try
            {
                if (IsBusy) return; // Double-check pattern
                IsBusy = true;

                var stopwatch = System.Diagnostics.Stopwatch.StartNew();
                
                using var scope = _sp.CreateScope();
                var db = scope.ServiceProvider.GetRequiredService<BestFlexDbContext>();

                var today = DateTime.Today;

                TodaySales = await db.SellingInvoiceItems
                    .AsNoTracking()
                    .Where(x => x.SellingInvoice.IssuedAt >= today)
                    .SumAsync(x => (decimal?) (x.Quantity * x.UnitPrice), ct) ?? 0m;

                var monthStart = new DateTime(today.Year, today.Month, 1);
                MonthSales = await db.SellingInvoiceItems
                    .AsNoTracking()
                    .Where(x => x.SellingInvoice.IssuedAt >= monthStart)
                    .SumAsync(x => (decimal?) (x.Quantity * x.UnitPrice), ct) ?? 0m;

                // Receivables = sum of all invoice item amounts (no payments modelled)
                TotalReceivables = await db.SellingInvoiceItems
                    .AsNoTracking()
                    .SumAsync(x => (decimal?) (x.Quantity * x.UnitPrice), ct) ?? 0m;

                // Sales by day
                SalesByDay.Clear();
                var grouped = await db.SellingInvoiceItems
                    .AsNoTracking()
                    .Where(x => x.SellingInvoice.IssuedAt >= from && x.SellingInvoice.IssuedAt <= to)
                    .GroupBy(x => x.SellingInvoice.IssuedAt.Date)
                    .Select(g => new { Day = g.Key, Total = g.Sum(x => (decimal)(x.Quantity * x.UnitPrice)) })
                    .ToListAsync(ct);

                var dict = grouped.ToDictionary(x => x.Day, x => x.Total);
                for (var d = from.Date; d <= to.Date; d = d.AddDays(1))
                {
                    dict.TryGetValue(d, out var total);
                    SalesByDay.Add(new ChartPointVm { Label = d, Total = total });
                }

                // Sales by customer (paged)
                SalesByCustomer.Clear();
                var custTake = Math.Max(1, _customerPaging.PageSize == 0 ? 10 : _customerPaging.PageSize);
                var custSkip = Math.Max(0, (_customerPaging.PageIndex - 1) * custTake);

                var byCustomer = await db.SellingInvoiceItems
                    .AsNoTracking()
                    .Where(x => x.SellingInvoice.IssuedAt >= from && x.SellingInvoice.IssuedAt <= to)
                    .GroupBy(x => new { x.SellingInvoice.CustomerAccountId, x.SellingInvoice.CustomerAccount.Name })
                    .Select(g => new { g.Key.CustomerAccountId, g.Key.Name, Total = g.Sum(x => (decimal)(x.Quantity * x.UnitPrice)) })
                    .OrderByDescending(x => x.Total)
                    .Skip(custSkip).Take(custTake)
                    .ToListAsync(ct);

                foreach (var c in byCustomer)
                    SalesByCustomer.Add(new ChartPointVm { Label = c.Name ?? string.Empty, Total = c.Total });
                _customerPaging.Update(_customerPaging.PageIndex, custTake, _customerPaging.TotalCount);
                
                stopwatch.Stop();
                await _audit.LogActionAsync("DASHBOARD_LOAD", null, null);
                // Log timing details via audit service details field
                await _audit.LogSecurityAsync("PERFORMANCE_METRIC", $"Dashboard load completed in {stopwatch.ElapsedMilliseconds}ms");
            }
            catch (Exception ex)
            {
                _error.Handle(ex, "DashboardViewModel.LoadAsync");
            }
            finally
            {
                IsBusy = false;
                _loadLock.Release();
            }
        }

        public async Task ReloadLowAsync(int threshold, int topN, CancellationToken ct = default)
        {
            using var scope = _sp.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<BestFlexDbContext>();
            var svc = new InventoryReadService(db);

                var list = await svc.GetLowStockAsync(threshold, topN, ct);
            var total = await svc.CountLowStockAsync(threshold, ct);

            LowStock.Clear();
            foreach (var row in list) LowStock.Add(row);

            LowSummary = $"Showing {LowStock.Count} of {total} items with stock ≤ {threshold}.";
            OnPropertyChanged(nameof(LowSummary));
        }

        public async Task ReloadDebtAsync(int topN, CancellationToken ct = default)
        {
            using var scope = _sp.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<BestFlexDbContext>();
            var svc = new SalesReadService(db);

            var list = await svc.GetTopOutstandingAsync(topN, ct);

            TopDebt.Clear();
            foreach (var row in list) TopDebt.Add(row);

            var totalAmount = TopDebt.Sum(x => x.Amount);
            DebtSummary = $"Showing top {TopDebt.Count} customers · Total amount: {totalAmount:N2}";
            OnPropertyChanged(nameof(DebtSummary));
        }

        public async Task ReloadAllAsync(int threshold, int lowTopN, int debtTopN, DateTime from, DateTime to, CancellationToken ct = default)
        {
            await ReloadLowAsync(threshold, lowTopN, ct);
            await ReloadDebtAsync(debtTopN, ct);
            await LoadAsync(from, to, ct);
        }

        private async Task RefreshAsync()
        {
            await LoadAsync(_currentFrom, _currentTo);
        }

        private async Task RefreshLowStockAsync()
        {
            await ReloadLowAsync(_currentThreshold, _currentLowTopN);
        }

        private async Task RefreshDebtAsync()
        {
            await ReloadDebtAsync(_currentDebtTopN);
        }

        private async Task LoadAllAsync()
        {
            await ReloadAllAsync(_currentThreshold, _currentLowTopN, _currentDebtTopN, _currentFrom, _currentTo);
        }

        private async Task OpenLowStock()
        {
            if (CanViewLowStock)
            {
                await _audit.LogNavigationAsync("LowStock");
                _nav.OpenLowStock(_currentThreshold);
            }
        }

        private async Task OpenDebt()
        {
            if (CanViewDebt)
            {
                await _audit.LogNavigationAsync("UnpaidInvoices");
                _nav.OpenUnpaidInvoices(_currentDebtTopN);
            }
        }
    }
}
