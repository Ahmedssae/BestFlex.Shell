using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using BestFlex.Infrastructure.Services;
using BestFlex.Persistence.Data;
using Microsoft.Extensions.DependencyInjection;

namespace BestFlex.Shell.ViewModels
{
    public sealed class UnpaidInvoicesViewModel : ViewModelBase
    {
        private readonly IServiceProvider _sp;
        private readonly Infrastructure.PaginationState _paging = new();

        public UnpaidInvoicesViewModel(IServiceProvider sp)
        {
            _sp = sp ?? throw new ArgumentNullException(nameof(sp));
        }

        public ObservableCollection<UnpaidCustomerVm> Items { get; } = new();
        public int PageIndex { get => _paging.PageIndex; set { _paging.Update(Math.Max(1,value), _paging.PageSize, _paging.TotalCount); OnPropertyChanged(nameof(PageIndex)); } }
        public int PageSize { get => _paging.PageSize; set { _paging.Update(1, Math.Max(1,value), _paging.TotalCount); OnPropertyChanged(nameof(PageSize)); } }

        private decimal _totalOutstanding;
        public decimal TotalOutstanding { get => _totalOutstanding; private set => SetProperty(ref _totalOutstanding, value); }

        private bool _isBusy;
        public bool IsBusy { get => _isBusy; private set => SetProperty(ref _isBusy, value); }

        public async Task LoadAsync(int topN, CancellationToken ct = default)
        {
            if (IsBusy) return;
            try
            {
                IsBusy = true;

                using var scope = _sp.CreateScope();
                var db = scope.ServiceProvider.GetRequiredService<BestFlexDbContext>();
                var svc = new SalesReadService(db);

                // simple paging: take page-size entries starting at page index
                var take = Math.Max(1, PageSize == 0 ? 10 : PageSize);
                var list = await svc.GetTopOutstandingAsync(Math.Max(1, take), ct).ConfigureAwait(false);

                Items.Clear();
                foreach (var c in list)
                {
                    Items.Add(new UnpaidCustomerVm
                    {
                        CustomerAccountId = c.CustomerAccountId,
                        CustomerName = c.CustomerName,
                        InvoiceCount = c.InvoiceCount,
                        Amount = c.Amount,
                    });
                }

                TotalOutstanding = Items.Sum(x => x.Amount);
                _paging.Update(_paging.PageIndex, take, Items.Count);
            }
            finally
            {
                IsBusy = false;
            }
        }

        public ObservableCollection<InvoiceVm> Invoices { get; } = new();

        public async Task LoadInvoicesForCustomerAsync(int customerAccountId, CancellationToken ct = default)
        {
            using var scope = _sp.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<BestFlexDbContext>();
            var svc = new SalesReadService(db);

            var list = await svc.GetInvoicesForCustomerAsync(customerAccountId, ct).ConfigureAwait(false);

            Invoices.Clear();
            foreach (var i in list)
            {
                Invoices.Add(new InvoiceVm
                {
                    InvoiceId = i.InvoiceId,
                    InvoiceNo = i.InvoiceNo,
                    IssuedAt = i.IssuedAt,
                    Amount = i.Amount,
                    Currency = i.Currency
                });
            }
        }

        public sealed class InvoiceVm
        {
            public int InvoiceId { get; set; }
            public string InvoiceNo { get; set; } = string.Empty;
            public DateTime IssuedAt { get; set; }
            public decimal Amount { get; set; }
            public string Currency { get; set; } = "USD";
        }

        public sealed class UnpaidCustomerVm
        {
            public int CustomerAccountId { get; set; }
            public string CustomerName { get; set; } = string.Empty;
            public int InvoiceCount { get; set; }
            public decimal Amount { get; set; }
        }
    }
}
