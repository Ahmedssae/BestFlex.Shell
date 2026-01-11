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
        private readonly ObservableCollection<InventoryReadService.LowStockDto> _rows = new();

        public LowStockWindow(int threshold)
        {
            InitializeComponent();
            grid.ItemsSource = _rows;
            txtThreshold.Text = threshold.ToString(CultureInfo.InvariantCulture);
            Loaded += async (_, __) => await ReloadAsync();
        }

        private int Threshold
        {
            get
            {
                var s = (txtThreshold.Text ?? "").Trim();
                return int.TryParse(s, NumberStyles.Integer, CultureInfo.InvariantCulture, out var n) ? n : 5;
            }
        }

        private async Task ReloadAsync(CancellationToken ct = default)
        {
            try
            {
                var sp = ((App)System.Windows.Application.Current).Services;
                using var scope = sp.CreateScope();
                var db = scope.ServiceProvider.GetRequiredService<BestFlexDbContext>();
                var svc = new InventoryReadService(db);

                // cap at 2000 to avoid accidental huge grids
                var list = await svc.GetAllLowStockAsync(Threshold, cap: 2000, ct);
                var total = await svc.CountLowStockAsync(Threshold, ct);

                _rows.Clear();
                foreach (var row in list) _rows.Add(row);

                txtSummary.Text = $"Total low-stock items: {total}";
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
