using System;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using BestFlex.Infrastructure.Services;
using BestFlex.Persistence.Data;
using Microsoft.Extensions.DependencyInjection;

namespace BestFlex.Shell.Windows
{
    public partial class LowStockWindow : Window
    {
        private readonly ViewModels.LowStockViewModel _vm;
        private readonly int _threshold;

        public LowStockWindow(int threshold)
        {
            InitializeComponent();
            _threshold = threshold;
            _vm = ((App)System.Windows.Application.Current).Services.GetRequiredService<ViewModels.LowStockViewModel>();
            DataContext = _vm;
            // bind grid in XAML to Items; keep threshold text for display
            txtThreshold.Text = threshold.ToString(System.Globalization.CultureInfo.InvariantCulture);
            Loaded += async (_, __) => await ReloadAsync();
        }

        private int Threshold => _threshold;

        private async Task ReloadAsync(CancellationToken ct = default)
        {
            await _vm.LoadAsync(Threshold, cap: 2000, ct);
            // window is UI-only: summary binding may be in XAML; keep existing txtSummary update for parity
            txtSummary.Text = $"Total low-stock items: {_vm.Total}";
        }

        private async void Refresh_Click(object sender, RoutedEventArgs e) => await ReloadAsync();
    }
}
