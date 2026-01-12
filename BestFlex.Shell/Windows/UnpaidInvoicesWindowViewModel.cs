using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using BestFlex.Infrastructure.Services;
using BestFlex.Persistence.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace BestFlex.Shell.Windows
{
    public sealed class UnpaidInvoicesWindowViewModel
    {
        private readonly IServiceProvider _sp;

        public ObservableCollection<SalesReadService.CustomerOutstandingDto> Customers { get; } = new();
        public ObservableCollection<SalesReadService.InvoiceSummaryDto> Invoices { get; } = new();

        public UnpaidInvoicesWindowViewModel(IServiceProvider sp)
        {
            _sp = sp ?? throw new ArgumentNullException(nameof(sp));
        }

        public async Task LoadAsync(int topN, CancellationToken ct = default)
        {
            using var scope = _sp.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<BestFlexDbContext>();
            var svc = new SalesReadService(db);

            var top = await svc.GetTopOutstandingAsync(topN, ct);
            Customers.Clear();
            foreach (var c in top) Customers.Add(c);
        }

        public async Task LoadInvoicesForCustomerAsync(int customerAccountId, CancellationToken ct = default)
        {
            using var scope = _sp.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<BestFlexDbContext>();
            var svc = new SalesReadService(db);

            var list = await svc.GetInvoicesForCustomerAsync(customerAccountId, ct);
            Invoices.Clear();
            foreach (var inv in list) Invoices.Add(inv);
        }
    }
}
