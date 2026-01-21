using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Input;
using BestFlex.Infrastructure;
using BestFlex.Persistence.Data;
using BestFlex.Shell.ViewModels;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace BestFlex.Shell.Pages
{
    // ViewModel that contains data access and paging logic for InvoicesPage.
    public sealed class InvoicesPageViewModel : ViewModelBase
    {
        private readonly IServiceProvider _sp;
        private readonly AsyncRelayCommand _loadCmd;
        private readonly AsyncRelayCommand _searchCmd;
        private readonly AsyncRelayCommand _nextPageCmd;
        private readonly AsyncRelayCommand _prevPageCmd;
        
        private bool _isBusy;
        public bool IsBusy { get => _isBusy; private set => SetProperty(ref _isBusy, value); }

        public ObservableCollection<InvoiceRow> Rows { get; } = new();

        private int _page = 1;
        public int Page { get => _page; set { if (_page == value) return; _page = Math.Max(1, value); OnPropertyChanged(); OnPropertyChanged(nameof(PageIndicatorText)); UpdateCommandStates(); } }
        
        public int PageSize { get => _pageSize; set { if (_pageSize == value) return; _pageSize = Math.Max(1, value); OnPropertyChanged(); _ = LoadAsync(); UpdateCommandStates(); } }
        private int _pageSize = 25;
        
        public int Total { get; private set; } = 0;
        public int TotalPages => Math.Max(1, (int)Math.Ceiling(Total / (double)PageSize));
        public string PageIndicatorText => $"Page {Page} of {TotalPages}";
        
        public ICommand LoadCommand => _loadCmd;
        public ICommand SearchCommand => _searchCmd;
        public ICommand NextPageCommand => _nextPageCmd;
        public ICommand PreviousPageCommand => _prevPageCmd;

        // Filters
        private string _numberFilter = string.Empty;
        public string NumberFilter { get => _numberFilter; set { if (_numberFilter == value) return; _numberFilter = value; OnPropertyChanged(); } }
        
        private string _customerFilter = string.Empty;
        public string CustomerFilter { get => _customerFilter; set { if (_customerFilter == value) return; _customerFilter = value; OnPropertyChanged(); } }
        
        public DateTime? From { get; set; }
        public DateTime? To { get; set; }

        public InvoicesPageViewModel(IServiceProvider sp)
        {
            _sp = sp ?? throw new ArgumentNullException(nameof(sp));
            
            _loadCmd = new AsyncRelayCommand(async () => await LoadAsync(), () => !IsBusy);
            _searchCmd = new AsyncRelayCommand(async () => { Page = 1; await LoadAsync(); }, () => !IsBusy);
            _nextPageCmd = new AsyncRelayCommand(async () => { Page++; await LoadAsync(); }, () => !IsBusy && Page < TotalPages);
            _prevPageCmd = new AsyncRelayCommand(async () => { Page--; await LoadAsync(); }, () => !IsBusy && Page > 1);
        }
        
        private void UpdateCommandStates()
        {
            try
            {
                _loadCmd?.RaiseCanExecuteChanged();
                _searchCmd?.RaiseCanExecuteChanged();
                _nextPageCmd?.RaiseCanExecuteChanged();
                _prevPageCmd?.RaiseCanExecuteChanged();
            }
            catch { }
        }

        public async Task LoadAsync(CancellationToken ct = default)
        {
            if (IsBusy) return;
            try
            {
                IsBusy = true;
                
                using var scope = _sp.CreateScope();
                var db = scope.ServiceProvider.GetRequiredService<BestFlexDbContext>();

            var q =
                from inv in db.SellingInvoices.AsNoTracking()
                join ca in db.CustomerAccounts.AsNoTracking() on inv.CustomerAccountId equals ca.Id
                select new { inv, ca };

            var num = (NumberFilter ?? string.Empty).Trim();
            if (!string.IsNullOrEmpty(num)) q = q.Where(x => x.inv.InvoiceNo.Contains(num));

            var cust = (CustomerFilter ?? string.Empty).Trim();
            if (!string.IsNullOrEmpty(cust)) q = q.Where(x => EF.Functions.Like(x.ca.Name, $"%{cust}%"));

            if (From.HasValue)
            {
                var from = From.Value.Date;
                q = q.Where(x => x.inv.IssuedAt >= from);
            }
            if (To.HasValue)
            {
                var to = To.Value.Date.AddDays(1).AddTicks(-1);
                q = q.Where(x => x.inv.IssuedAt <= to);
            }

            Total = await q.CountAsync(ct);

            var pageQuery = q
                .OrderByDescending(x => x.inv.IssuedAt)
                .Skip((Page - 1) * PageSize)
                .Take(PageSize)
                .Select(x => new
                {
                    x.inv.Id,
                    x.inv.InvoiceNo,
                    x.inv.IssuedAt,
                    CustomerName = x.ca.Name,
                    Currency = x.inv.Currency,
                    Items = db.SellingInvoiceItems.Count(i => i.SellingInvoiceId == x.inv.Id),
                    AmountDouble = db.SellingInvoiceItems
                        .Where(i => i.SellingInvoiceId == x.inv.Id)
                        .Sum(i => (double)(i.Quantity * i.UnitPrice))
                });

            var rows = (await pageQuery.ToListAsync(ct))
                .Select(r => new InvoiceRow(
                    r.Id,
                    r.InvoiceNo,
                    r.IssuedAt,
                    r.CustomerName,
                    r.Items,
                    (decimal)r.AmountDouble,
                    r.Currency ?? "USD"))
                .ToList();

            Rows.Clear();
            foreach (var r in rows) Rows.Add(r);
            
            OnPropertyChanged(nameof(Total));
            OnPropertyChanged(nameof(TotalPages));
            OnPropertyChanged(nameof(PageIndicatorText));
            UpdateCommandStates();
            }
            finally
            {
                IsBusy = false;
                UpdateCommandStates();
            }
        }

    public sealed record InvoiceRow(
        int Id,
        string InvoiceNo,
        DateTime IssuedAt,
        string CustomerName,
        int Items,
        decimal Amount,
        string Currency
    );
    }
}
