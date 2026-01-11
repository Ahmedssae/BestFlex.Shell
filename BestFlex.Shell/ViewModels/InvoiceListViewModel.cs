using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;

namespace BestFlex.Shell.ViewModels
{
    public class InvoiceListViewModel : INotifyPropertyChanged
    {
        // ---- Models shown in the grid ----
        public class InvoiceRow
        {
            public int Id { get; set; }
            public string InvoiceNo { get; set; } = "";
            public DateTime IssuedAt { get; set; }
            public string Customer { get; set; } = "";
            public int ItemsCount { get; set; }
            public decimal Total { get; set; }
            public string Currency { get; set; } = "";
        }

        public class CustomerOption
        {
            public int Id { get; set; }
            public string Name { get; set; } = "";
        }

        // ---- Pagination DTO ----
        public record PagedResult<T>(ReadOnlyCollection<T> Items, int TotalCount);

        // ---- Filters ----
        private DateTime? _startDate = DateTime.Today.AddMonths(-1);
        public DateTime? StartDate { get => _startDate; set { _startDate = value; OnChanged(); } }

        private DateTime? _endDate = DateTime.Today;
        public DateTime? EndDate { get => _endDate; set { _endDate = value; OnChanged(); } }

        public ObservableCollection<CustomerOption> Customers { get; } = new();
        private CustomerOption? _selectedCustomer;
        public CustomerOption? SelectedCustomer { get => _selectedCustomer; set { _selectedCustomer = value; OnChanged(); } }

        private string? _numberLike;
        public string? NumberLike { get => _numberLike; set { _numberLike = value; OnChanged(); } }

        // ---- List ----
        public ObservableCollection<InvoiceRow> Invoices { get; } = new();

        // ---- Pagination ----
        public ObservableCollection<int> PageSizeOptions { get; } = new(new[] { 10, 25, 50, 100 });
        private int _pageSize = 25;
        public int PageSize
        {
            get => _pageSize;
            set { if (value <= 0) return; _pageSize = value; OnChanged(); _ = SearchAsync(resetToFirstPage: true); }
        }

        private int _pageNumber = 1; // 1-based
        public int PageNumber { get => _pageNumber; private set { _pageNumber = Math.Max(1, value); OnChanged(); OnChanged(nameof(PageIndicatorText)); } }

        private int _totalCount;
        public int TotalCount { get => _totalCount; private set { _totalCount = value; OnChanged(); OnChanged(nameof(TotalPages)); OnChanged(nameof(TotalSummaryText)); OnChanged(nameof(HasPrevPage)); OnChanged(nameof(HasNextPage)); OnChanged(nameof(PageIndicatorText)); } }

        public int TotalPages => Math.Max(1, (int)Math.Ceiling(TotalCount / (double)PageSize));

        public bool HasPrevPage => PageNumber > 1;
        public bool HasNextPage => PageNumber < TotalPages;

        private string _goToPageText = "1";
        internal DateTime FromDate;
        internal DateTime ToDate;

        public string GoToPageText { get => _goToPageText; set { _goToPageText = value; OnChanged(); } }

        public string PageIndicatorText => $"Page {PageNumber} / {TotalPages}";
        public string TotalSummaryText => $"{TotalCount:N0} invoice(s) • {PageIndicatorText}";

        // ---- Data source (inject your service/repository here) ----
        private readonly IDataSource _data;

        public interface IDataSource
        {
            Task<ReadOnlyCollection<CustomerOption>> LoadCustomersAsync();
            Task<PagedResult<InvoiceRow>> QueryInvoicesAsync(DateTime? start, DateTime? end, int? customerId, string? numberLike, int pageNumber, int pageSize);
        }

        // Constructor (IDataSource should be registered in DI)
        public InvoiceListViewModel(IDataSource dataSource)
        {
            _data = dataSource;
        }

        // ---- Public API for the Window ----
        public async Task LoadAsync()
        {
            // Customers
            Customers.Clear();
            foreach (var c in await _data.LoadCustomersAsync())
                Customers.Add(c);

            await SearchAsync(resetToFirstPage: true);
        }

        public async Task SearchAsync(bool resetToFirstPage = false)
        {
            if (resetToFirstPage) PageNumber = 1;
            await LoadPageAsync(PageNumber);
        }

        public async Task GoPrevAsync()
        {
            if (!HasPrevPage) return;
            PageNumber--;
            await LoadPageAsync(PageNumber);
        }

        public async Task GoNextAsync()
        {
            if (!HasNextPage) return;
            PageNumber++;
            await LoadPageAsync(PageNumber);
        }

        public async Task GoToPageAsync()
        {
            if (!int.TryParse(GoToPageText, out var requested) || requested < 1)
                requested = 1;
            requested = Math.Min(requested, TotalPages == 0 ? 1 : TotalPages);
            if (requested == PageNumber) return;
            PageNumber = requested;
            await LoadPageAsync(PageNumber);
        }

        // ---- Core loader ----
        private async Task LoadPageAsync(int page)
        {
            var customerId = SelectedCustomer?.Id;
            var result = await _data.QueryInvoicesAsync(StartDate, EndDate, customerId, NumberLike, page, PageSize);

            Invoices.Clear();
            foreach (var row in result.Items)
                Invoices.Add(row);

            TotalCount = result.TotalCount;

            // keep textbox in sync
            GoToPageText = PageNumber.ToString();
        }

        // ---- INotifyPropertyChanged ----
        public event PropertyChangedEventHandler? PropertyChanged;
        private void OnChanged([CallerMemberName] string? name = null)
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }
}
