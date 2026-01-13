using System;
using System.Collections.ObjectModel;
using System.Threading;
using System.Threading.Tasks;
using BestFlex.Infrastructure.Services;
using BestFlex.Persistence.Data;
using Microsoft.Extensions.DependencyInjection;

namespace BestFlex.Shell.ViewModels
{
    public sealed class LowStockViewModel : ViewModelBase
    {
        private readonly IServiceProvider _sp;
        private readonly Infrastructure.PaginationState _paging = new();

        public LowStockViewModel(IServiceProvider sp)
        {
            _sp = sp ?? throw new ArgumentNullException(nameof(sp));
        }

        public ObservableCollection<LowStockItemVm> Items { get; } = new();
        public int PageIndex { get => _paging.PageIndex; set { _paging.Update(Math.Max(1,value), _paging.PageSize, _paging.TotalCount); OnPropertyChanged(nameof(PageIndex)); } }
        public int PageSize { get => _paging.PageSize; set { _paging.Update(1, Math.Max(1,value), _paging.TotalCount); OnPropertyChanged(nameof(PageSize)); } }

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
            }
            finally
            {
                IsBusy = false;
            }
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
