using System;
using System.Collections.ObjectModel;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Input;
using BestFlex.Infrastructure.Services;
using BestFlex.Persistence.Data;
using Microsoft.Extensions.DependencyInjection;

namespace BestFlex.Shell.ViewModels
{
    public sealed class LowStockViewModel : ViewModelBase
    {
        private readonly IServiceProvider _sp;
        private readonly Infrastructure.PaginationState _paging = new();

        private readonly AsyncRelayCommand _refreshCmd;
        private readonly AsyncRelayCommand _nextPageCmd;
        private readonly AsyncRelayCommand _prevPageCmd;

        public LowStockViewModel(IServiceProvider sp)
        {
            _sp = sp ?? throw new ArgumentNullException(nameof(sp));
            
            _refreshCmd = new AsyncRelayCommand(async () => await LoadAsync(_currentThreshold, _currentCap), () => !IsBusy);
            _nextPageCmd = new AsyncRelayCommand(async () => await GoToPageAsync(PageIndex + 1), () => !IsBusy && PageIndex < TotalPages);
            _prevPageCmd = new AsyncRelayCommand(async () => await GoToPageAsync(PageIndex - 1), () => !IsBusy && PageIndex > 1);
        }

        private int _currentThreshold = 10;
        private int _currentCap = 2000;
        
        public ObservableCollection<LowStockItemVm> Items { get; } = new();
        public int PageIndex { get => _paging.PageIndex; set { _paging.Update(Math.Max(1,value), _paging.PageSize, _paging.TotalCount); OnPropertyChanged(nameof(PageIndex)); OnPropertyChanged(nameof(PageIndicatorText)); UpdateCommandStates(); } }
        public int PageSize { get => _paging.PageSize; set { _paging.Update(1, Math.Max(1,value), _paging.TotalCount); OnPropertyChanged(nameof(PageSize)); UpdateCommandStates(); } }
        
        public int TotalPages => Math.Max(1, (int)Math.Ceiling(Total / (double)PageSize));
        public string PageIndicatorText => $"Page {PageIndex} of {TotalPages}";
        
        public ICommand RefreshCommand => _refreshCmd;
        public ICommand NextPageCommand => _nextPageCmd;
        public ICommand PreviousPageCommand => _prevPageCmd;

        private bool _isBusy;
        public bool IsBusy { get => _isBusy; private set => SetProperty(ref _isBusy, value); }

        private int _total;
        public int Total { get => _total; private set => SetProperty(ref _total, value); }

        public async Task LoadAsync(int threshold, int cap = 2000, CancellationToken ct = default)
        {
            if (IsBusy) return;
            try
            {
                IsBusy = true;
                _currentThreshold = threshold;
                _currentCap = cap;

                using var scope = _sp.CreateScope();
                var db = scope.ServiceProvider.GetRequiredService<BestFlexDbContext>();
                var svc = new InventoryReadService(db);

                // Use paging: server-side take/skip via service that supports 'take' and offset is (PageIndex-1)*PageSize
                var take = Math.Max(1, PageSize == 0 ? 50 : PageSize);
                var skip = Math.Max(0, (PageIndex - 1) * take);
                var list = await svc.GetLowStockAsync(threshold, take, ct).ConfigureAwait(false);
                var total = await svc.CountLowStockAsync(threshold, ct).ConfigureAwait(false);

                // update UI collection on caller context
                Items.Clear();
                foreach (var it in list)
                {
                    Items.Add(new LowStockItemVm
                    {
                        Id = it.Id,
                        Code = it.Code,
                        Name = it.Name,
                        StockQty = it.StockQty
                    });
                }

                Total = total;

                // update paging totals
                _paging.Update(_paging.PageIndex, take, total);
                OnPropertyChanged(nameof(PageIndex));
                OnPropertyChanged(nameof(PageSize));
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

        private void UpdateCommandStates()
        {
            try
            {
                _refreshCmd?.RaiseCanExecuteChanged();
                _nextPageCmd?.RaiseCanExecuteChanged();
                _prevPageCmd?.RaiseCanExecuteChanged();
            }
            catch { }
        }
        
        private async Task GoToPageAsync(int page)
        {
            if (page < 1) page = 1;
            var max = TotalPages == 0 ? 1 : TotalPages;
            if (page > max) page = max;
            if (page == PageIndex) return;
            PageIndex = page;
            await LoadAsync(_currentThreshold, _currentCap);
        }

        public sealed class LowStockItemVm
        {
            public int Id { get; set; }
            public string Code { get; set; } = string.Empty;
            public string Name { get; set; } = string.Empty;
            public decimal StockQty { get; set; }
        }
    }
}
