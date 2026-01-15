using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Input;
using BestFlex.Shell.Infrastructure;

namespace BestFlex.Shell.ViewModels
{
    public sealed class InvoiceListViewModel : INotifyPropertyChanged
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
            Task<PagedResult<InvoiceRow>> QueryInvoicesAsync(DateTime? start, DateTime? end, int? customerId, string? numberLike, int pageNumber, int pageSize);
        }

        private readonly IDataSource _data;
        private readonly BestFlex.Application.Abstractions.INavigationService _nav;
        private readonly PaginationState _paging = new();

        private readonly AsyncRelayCommand<int> _openInvoiceCmd;

        public InvoiceListViewModel(IDataSource dataSource, BestFlex.Application.Abstractions.INavigationService nav)
        {
            _data = dataSource ?? throw new ArgumentNullException(nameof(dataSource));
            _nav = nav ?? throw new ArgumentNullException(nameof(nav));

            _nextPageCmd = new AsyncRelayCommand(async () => await GoToPageAsync(PageIndex + 1), () => !IsBusy && PageIndex < TotalPages);
            _prevPageCmd = new AsyncRelayCommand(async () => await GoToPageAsync(PageIndex - 1), () => !IsBusy && PageIndex > 1);
            _goToPageCmd = new AsyncRelayCommand<int>(async p => await GoToPageAsync(p), p => !IsBusy && p >= 1 && p <= TotalPages);
            _searchCmd = new AsyncRelayCommand(async () => { PageIndex = 1; await LoadAsync(); });

            _openInvoiceCmd = new AsyncRelayCommand<int>(async id => await Task.Run(() => _nav.OpenInvoiceDetails(id)), id => id > 0);

            // default paging
            PageSize = 25;
            PageIndex = 1;
        }

        // Collections
        public ObservableCollection<InvoiceRow> Items { get; } = new();
        public ObservableCollection<CustomerOption> Customers { get; } = new();

        // Filters
        private DateTime? _startDate = DateTime.Today.AddMonths(-1);
        public DateTime? StartDate { get => _startDate; set { if (_startDate == value) return; _startDate = value; OnChanged(); } }

        private DateTime? _endDate = DateTime.Today;
        public DateTime? EndDate { get => _endDate; set { if (_endDate == value) return; _endDate = value; OnChanged(); } }

        private CustomerOption? _selectedCustomer;
        public CustomerOption? SelectedCustomer { get => _selectedCustomer; set { if (_selectedCustomer == value) return; _selectedCustomer = value; OnChanged(); } }

        private string? _numberLike;
        public string? NumberLike { get => _numberLike; set { if (_numberLike == value) return; _numberLike = value; OnChanged(); } }

        // State
        private bool _isBusy;
        public bool IsBusy { get => _isBusy; private set { if (_isBusy == value) return; _isBusy = value; OnChanged(); UpdateCommandStates(); } }

        public int PageIndex { get => _paging.PageIndex; private set { _paging.Update(Math.Max(1, value), _paging.PageSize, _paging.TotalCount); OnChanged(nameof(PageIndex)); OnChanged(nameof(PageIndicatorText)); UpdateCommandStates(); } }
        public int PageSize { get => _paging.PageSize; set { _paging.Update(1, Math.Max(1, value), _paging.TotalCount); OnChanged(nameof(PageSize)); _ = LoadAsync(); UpdateCommandStates(); } }
        public int TotalCount { get => _paging.TotalCount; private set { _paging.Update(_paging.PageIndex, _paging.PageSize, Math.Max(0, value)); OnChanged(nameof(TotalCount)); OnChanged(nameof(TotalPages)); OnChanged(nameof(PageIndicatorText)); UpdateCommandStates(); } }

        public int TotalPages => Math.Max(1, (int)Math.Ceiling(TotalCount / (double)PageSize));
        public string PageIndicatorText => $"Page {PageIndex} of {TotalPages}";

        // Commands
        private readonly AsyncRelayCommand _nextPageCmd;
        private readonly AsyncRelayCommand _prevPageCmd;
        private readonly AsyncRelayCommand _searchCmd;
        private readonly AsyncRelayCommand<int> _goToPageCmd;

        public ICommand NextPageCommand => _nextPageCmd;
        public ICommand PreviousPageCommand => _prevPageCmd;
        public ICommand SearchCommand => _searchCmd;
        public ICommand GoToPageCommand => _goToPageCmd;
        public ICommand OpenInvoiceCommand => _openInvoiceCmd;

        private void UpdateCommandStates()
        {
            try
            {
                _nextPageCmd?.RaiseCanExecuteChanged();
                _prevPageCmd?.RaiseCanExecuteChanged();
                _goToPageCmd?.RaiseCanExecuteChanged();
            }
            catch { }
        }

        // Loading
        public async Task LoadAsync(CancellationToken ct = default)
        {
            if (IsBusy) return;
            IsBusy = true;
            try
            {
                Customers.Clear();
                var custs = await _data.LoadCustomersAsync();
                foreach (var c in custs) Customers.Add(c);

                if (PageIndex < 1) PageIndex = 1;

                var res = await _data.QueryInvoicesAsync(StartDate, EndDate, SelectedCustomer?.Id, NumberLike, PageIndex, PageSize);

                Items.Clear();
                foreach (var r in res.Items) Items.Add(r);

                TotalCount = res.TotalCount;
            }
            finally
            {
                UpdateCommandStates();
                IsBusy = false;
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

        public event PropertyChangedEventHandler? PropertyChanged;
        private void OnChanged([CallerMemberName] string? name = null) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }
}
