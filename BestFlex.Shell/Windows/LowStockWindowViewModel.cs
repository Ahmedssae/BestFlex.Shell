using System;
using System.Collections.ObjectModel;
using System.Threading;
using System.Threading.Tasks;
using BestFlex.Infrastructure.Services;
using BestFlex.Persistence.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace BestFlex.Shell.Windows
{
    /// <summary>
    /// ViewModel for LowStockWindow: loads low-stock items and exposes them for the view.
    /// </summary>
    public sealed class LowStockWindowViewModel
    {
        private readonly IServiceProvider _sp;

        public ObservableCollection<InventoryReadService.LowStockDto> Rows { get; } = new();

        public int Total { get; private set; }

        public LowStockWindowViewModel(IServiceProvider sp)
        {
            _sp = sp ?? throw new ArgumentNullException(nameof(sp));
        }

        public async Task LoadAsync(int threshold, int cap = 2000, CancellationToken ct = default)
        {
            using var scope = _sp.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<BestFlexDbContext>();
            var svc = new InventoryReadService(db);

            var list = await svc.GetAllLowStockAsync(threshold, cap: cap, ct);
            var total = await svc.CountLowStockAsync(threshold, ct);

            Rows.Clear();
            foreach (var row in list) Rows.Add(row);

            Total = total;
        }
    }
}
