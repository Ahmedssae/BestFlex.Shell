using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using BestFlex.Infrastructure.Services;
using BestFlex.Persistence.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace BestFlex.Shell.Pages
{
    /// <summary>
    /// ViewModel for DashboardPage: encapsulates data loading for low-stock and top-debt lists.
    /// Contains only data access and aggregation; UI formatting remains in the view.
    /// </summary>
    public sealed class DashboardPageViewModel
    {
        private readonly IServiceProvider _sp;

        public ObservableCollection<InventoryReadService.LowStockDto> LowStock { get; } = new();
        public ObservableCollection<SalesReadService.CustomerOutstandingDto> TopDebt { get; } = new();

        public string LowSummary { get; private set; } = string.Empty;
        public string DebtSummary { get; private set; } = string.Empty;

        public DashboardPageViewModel(IServiceProvider sp)
        {
            _sp = sp ?? throw new ArgumentNullException(nameof(sp));
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

            LowSummary = $"Showing {LowStock.Count} of {total} items with stock ? {threshold}.";
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
        }

        public async Task ReloadAllAsync(int threshold, int lowTopN, int debtTopN, CancellationToken ct = default)
        {
            await ReloadLowAsync(threshold, lowTopN, ct);
            await ReloadDebtAsync(debtTopN, ct);
        }
    }
}
