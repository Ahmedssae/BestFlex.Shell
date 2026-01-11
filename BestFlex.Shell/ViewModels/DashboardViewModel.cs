using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using BestFlex.Infrastructure.Queries;

namespace BestFlex.Shell.ViewModels
{
    public sealed class DashboardViewModel : INotifyPropertyChanged
    {
        private readonly DashboardQueryService _svc;
        public DashboardViewModel(DashboardQueryService svc) => _svc = svc;

        private decimal _today;
        public decimal Today { get => _today; set { _today = value; OnChanged(); } }

        private decimal _month;
        public decimal ThisMonth { get => _month; set { _month = value; OnChanged(); } }

        public ObservableCollection<DashboardQueryService.SalesPoint> Sales { get; } = new();
        public ObservableCollection<DashboardQueryService.LowStockRow> LowStock { get; } = new();

        public async Task LoadAsync()
        {
            var totals = await _svc.GetSalesTotalsAsync();
            Today = totals.Today;
            ThisMonth = totals.ThisMonth;

            Sales.Clear();
            foreach (var p in await _svc.GetDailySalesAsync(14))
                Sales.Add(p);

            LowStock.Clear();
            foreach (var r in await _svc.GetLowStockAsync(5, 10))
                LowStock.Add(r);
        }

        public event PropertyChangedEventHandler? PropertyChanged;
        private void OnChanged([CallerMemberName] string? name = null)
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }
}
