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
        private readonly LowStockWindowViewModel _vm;
        private readonly int _threshold;

        public LowStockWindow(int threshold)
        {
            InitializeComponent();
            _threshold = threshold;
            _vm = new LowStockWindowViewModel(((App)System.Windows.Application.Current).Services);
            grid.ItemsSource = _vm.Rows;
            txtThreshold.Text = threshold.ToString(System.Globalization.CultureInfo.InvariantCulture);
            Loaded += async (_, __) => await ReloadAsync();
        }

        private int Threshold => _threshold;

        private async Task ReloadAsync(CancellationToken ct = default)
        {
            try
            {
                await _vm.LoadAsync(Threshold, cap: 2000, ct);
                txtSummary.Text = $"Total low-stock items: {_vm.Total}";
            }
            catch (Exception ex)
            {
                // Here 'this' IS a Window, so this overload is valid
                MessageBox.Show(this, $"Failed to load low stock list.\n\n{ex.Message}", "Low Stock", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private async void Refresh_Click(object sender, RoutedEventArgs e) => await ReloadAsync();
    }
}
