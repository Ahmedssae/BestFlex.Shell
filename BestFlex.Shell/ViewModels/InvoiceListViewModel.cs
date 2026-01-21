using System;
using System.Collections.ObjectModel;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Input;
using BestFlex.Application.Abstractions;
using BestFlex.Infrastructure.Services;
using BestFlex.Persistence.Data;
using Microsoft.Extensions.DependencyInjection;

namespace BestFlex.Shell.ViewModels
{
    public sealed class InvoiceListViewModel : ViewModelBase
    {
        public class InvoiceRow
        {
            public int Id { get; set; }
            public string InvoiceNo { get; set; } = string.Empty;
            public DateTime IssuedAt { get; set; }
            public string Customer { get; set; } = string.Empty;
            public int ItemsCount { get; set; }
            public decimal Total { get; set; }
            public string Currency { get; set; } = string.Empty;
        }

        public class CustomerOption
        {
            public int Id { get; set; }
            public string Name { get; set; } = string.Empty;
        }

        public interface IDataSource
        {
            Task<ReadOnlyCollection<CustomerOption>> LoadCustomersAsync();
            Task<BestFlex.Shell.Infrastructure.PagedResult<InvoiceRow>> QueryInvoicesAsync(DateTime? start, DateTime? end, int? customerId, string? numberLike, int pageNumber, int pageSize);
        }

        private readonly IServiceProvider _sp;
        private readonly IPermissionService _permissions;
        private readonly IAuditService _audit;
        private readonly IErrorService _error;
        private readonly IDataSource _data;
        private readonly BestFlex.Application.Abstractions.INavigationService _nav;
        private readonly Infrastructure.PaginationState _paging = new();
        private readonly SemaphoreSlim _loadLock = new(1, 1);

        private readonly AsyncRelayCommand _loadCmd;
        private readonly AsyncRelayCommand _searchCmd;
        private readonly AsyncRelayCommand _nextPageCmd;
        private readonly AsyncRelayCommand _prevPageCmd;
        private readonly AsyncRelayCommand<int> _openInvoiceCmd;

        public InvoiceListViewModel(IServiceProvider sp, IDataSource dataSource, BestFlex.Application.Abstractions.INavigationService nav)
        {
            _sp = sp ?? throw new ArgumentNullException(nameof(sp));
            _data = dataSource ?? throw new ArgumentNullException(nameof(dataSource));
            _nav = nav ?? throw new ArgumentNullException(nameof(nav));
            _permissions = sp.GetRequiredService<IPermissionService>();
            _audit = sp.GetRequiredService<IAuditService>();
            _error = sp.GetRequiredService<IErrorService>();

            _loadCmd = new AsyncRelayCommand(async () => await LoadAsync(), () => !IsBusy && CanViewSales);
            _searchCmd = new AsyncRelayCommand(async () => { PageIndex = 1; await LoadAsync(); }, () => !IsBusy && CanViewSales);
            _nextPageCmd = new AsyncRelayCommand(async () => await GoToPageAsync(PageIndex + 1), () => !IsBusy && CanViewSales && PageIndex < TotalPages);
            _prevPageCmd = new AsyncRelayCommand(async () => await GoToPageAsync(PageIndex - 1), () => !IsBusy && CanViewSales && PageIndex > 1);
            _openInvoiceCmd = new AsyncRelayCommand<int>(async id => 
            {
                await _audit.LogActionAsync("INVOICE_OPENED", "SellingInvoice", id);
                _nav.OpenInvoiceDetails(id);
            }, id => id > 0 && CanOpenInvoice);

            // default paging
            PageSize = 25;
            PageIndex = 1;
        }

        // Collections
        public ObservableCollection<InvoiceRow> Items { get; } = new();
        public ObservableCollection<CustomerOption> Customers { get; } = new();

        // Filters
        private DateTime? _startDate = DateTime.Today.AddMonths(-1);
        public DateTime? StartDate { get => _startDate; set { if (_startDate == value) return; _startDate = value; OnPropertyChanged(); } }

        private DateTime? _endDate = DateTime.Today;
        public DateTime? EndDate { get => _endDate; set { if (_endDate == value) return; _endDate = value; OnPropertyChanged(); } }

        private CustomerOption? _selectedCustomer;
        public CustomerOption? SelectedCustomer { get => _selectedCustomer; set { if (_selectedCustomer == value) return; _selectedCustomer = value; OnPropertyChanged(); } }

        private string? _numberLike;
        public string? NumberLike { get => _numberLike; set { if (_numberLike == value) return; _numberLike = value; OnPropertyChanged(); } }

        // State
        private bool _isBusy;
        public bool IsBusy { get => _isBusy; private set => SetProperty(ref _isBusy, value); }

        public int PageIndex { get => _paging.PageIndex; private set { _paging.Update(Math.Max(1, value), _paging.PageSize, _paging.TotalCount); OnPropertyChanged(nameof(PageIndex)); OnPropertyChanged(nameof(PageIndicatorText)); UpdateCommandStates(); } }
        public int PageSize { get => _paging.PageSize; set { _paging.Update(1, Math.Max(1, value), _paging.TotalCount); OnPropertyChanged(nameof(PageSize)); _ = LoadAsync(); UpdateCommandStates(); } }
        public int TotalCount { get => _paging.TotalCount; private set { _paging.Update(_paging.PageIndex, _paging.PageSize, Math.Max(0, value)); OnPropertyChanged(nameof(TotalCount)); OnPropertyChanged(nameof(TotalPages)); OnPropertyChanged(nameof(PageIndicatorText)); UpdateCommandStates(); } }

        public int TotalPages => Math.Max(1, (int)Math.Ceiling(TotalCount / (double)PageSize));
        public string PageIndicatorText => $"Page {PageIndex} of {TotalPages}";

        // Permission properties
        public bool CanViewSales => _permissions.CanViewSales();
        public bool CanOpenInvoice => _permissions.CanOpenInvoice();
        
        // Commands
        public ICommand LoadCommand => _loadCmd;
        public ICommand NextPageCommand => _nextPageCmd;
        public ICommand PreviousPageCommand => _prevPageCmd;
        public ICommand SearchCommand => _searchCmd;
        public ICommand OpenInvoiceCommand => _openInvoiceCmd;

        private void UpdateCommandStates()
        {
            try
            {
                _nextPageCmd?.RaiseCanExecuteChanged();
                _prevPageCmd?.RaiseCanExecuteChanged();
            }
            catch { }
        }

        // Loading
        public async Task LoadAsync(CancellationToken ct = default)
        {
            if (IsBusy) return;
            
            await _loadLock.WaitAsync(ct);
            try
            {
                if (IsBusy) return; // Double-check pattern
                IsBusy = true;
                
                Customers.Clear();
                var custs = await _data.LoadCustomersAsync();
                foreach (var c in custs) Customers.Add(c);

                if (PageIndex < 1) PageIndex = 1;

                var res = await _data.QueryInvoicesAsync(StartDate, EndDate, SelectedCustomer?.Id, NumberLike, PageIndex, PageSize);

                Items.Clear();
                foreach (var r in res.Items) Items.Add(r);

                TotalCount = res.TotalCount;
            }
            catch (Exception ex)
            {
                _error.Handle(ex, "InvoiceListViewModel.LoadAsync");
            }
            finally
            {
                UpdateCommandStates();
                IsBusy = false;
                _loadLock.Release();
            }
        }

        public async Task SearchAsync(bool resetToFirstPage = false)
        {
            if (resetToFirstPage) PageIndex = 1;
            await LoadAsync();
        }

        private async Task GoToPageAsync(int page)
        {
            if (page < 1) page = 1;
            var max = TotalPages == 0 ? 1 : TotalPages;
            if (page > max) page = max;
            if (page == PageIndex) return;
            PageIndex = page;
            await LoadAsync();
        }

    }
}
