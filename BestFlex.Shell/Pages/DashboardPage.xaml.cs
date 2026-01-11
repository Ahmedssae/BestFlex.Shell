using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Globalization;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using BestFlex.Infrastructure.Services;
using BestFlex.Persistence.Data;
using Microsoft.Extensions.DependencyInjection;
using BestFlex.Shell.Infrastructure;

namespace BestFlex.Shell.Pages
{
    public partial class DashboardPage : UserControl, INotifyPropertyChanged
    {
        // Low stock
        public ObservableCollection<InventoryReadService.LowStockDto> LowStock { get; } = new();
        // Debt / Unpaid
        public ObservableCollection<SalesReadService.CustomerOutstandingDto> TopDebt { get; } = new();

        // Theme label text bound from XAML
        private string _themeText = "Light";
        public string ThemeText
        {
            get => _themeText;
            set { if (_themeText != value) { _themeText = value; OnPropertyChanged(); } }
        }

        public DashboardPage()
        {
            InitializeComponent();
            gridLow.ItemsSource = LowStock;
            gridDebt.ItemsSource = TopDebt;

            // Make bindings on this page work without extra ViewModel
            DataContext = this;

            ThemeText = UserPrefs.Current.Theme == "Dark" ? "Dark" : "Light";
        }

        private static int ParseInt(string? s, int fallback)
            => int.TryParse((s ?? "").Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var n) ? n : fallback;

        private int LowThreshold => ParseInt(txtThreshold?.Text, 5);
        private int LowTopN => GetComboValue(cmbTop, 10);
        private int DebtTopN => GetComboValue(cmbDebtTop, 10);

        private static int GetComboValue(ComboBox? combo, int fallback)
        {
            if (combo?.SelectedItem is ComboBoxItem it && int.TryParse(it.Content?.ToString(), out var n)) return n;
            return fallback;
        }

        private async Task ReloadLowAsync(CancellationToken ct = default)
        {
            try
            {
                var sp = ((App)System.Windows.Application.Current).Services;
                using var scope = sp.CreateScope();
                var db = scope.ServiceProvider.GetRequiredService<BestFlexDbContext>();
                var svc = new InventoryReadService(db);

                var list = await svc.GetLowStockAsync(LowThreshold, LowTopN, ct);
                var total = await svc.CountLowStockAsync(LowThreshold, ct);

                LowStock.Clear();
                foreach (var row in list) LowStock.Add(row);

                txtLowSummary.Text = $"Showing {LowStock.Count} of {total} items with stock ≤ {LowThreshold}.";
            }
            catch (Exception ex)
            {
                var owner = Window.GetWindow(this);
                if (owner != null)
                    MessageBox.Show(owner, $"Failed to load low stock items.\n\n{ex.Message}", "Dashboard", MessageBoxButton.OK, MessageBoxImage.Error);
                else
                    MessageBox.Show($"Failed to load low stock items.\n\n{ex.Message}", "Dashboard", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private async Task ReloadDebtAsync(CancellationToken ct = default)
        {
            try
            {
                var sp = ((App)System.Windows.Application.Current).Services;
                using var scope = sp.CreateScope();
                var db = scope.ServiceProvider.GetRequiredService<BestFlexDbContext>();
                var svc = new SalesReadService(db);

                var list = await svc.GetTopOutstandingAsync(DebtTopN, ct);

                TopDebt.Clear();
                foreach (var row in list) TopDebt.Add(row);

                var totalAmount = TopDebt.Sum(x => x.Amount);
                txtDebtSummary.Text = $"Showing top {TopDebt.Count} customers · Total amount: {totalAmount:N2}";
            }
            catch (Exception ex)
            {
                var owner = Window.GetWindow(this);
                if (owner != null)
                    MessageBox.Show(owner, $"Failed to load unpaid invoices.\n\n{ex.Message}", "Dashboard", MessageBoxButton.OK, MessageBoxImage.Error);
                else
                    MessageBox.Show($"Failed to load unpaid invoices.\n\n{ex.Message}", "Dashboard", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private async Task ReloadAllAsync()
        {
            await ReloadLowAsync();
            await ReloadDebtAsync();
        }

        // Events
        private async void UserControl_Loaded(object sender, RoutedEventArgs e) => await ReloadAllAsync();
        private async void Refresh_Click(object sender, RoutedEventArgs e) => await ReloadLowAsync();
        private async void RefreshDebt_Click(object sender, RoutedEventArgs e) => await ReloadDebtAsync();

        private void OpenLowFull_Click(object sender, RoutedEventArgs e)
        {
            var wnd = new Windows.LowStockWindow(LowThreshold) { Owner = Window.GetWindow(this) };
            wnd.ShowDialog();
        }

        private void OpenDebt_Click(object sender, RoutedEventArgs e)
        {
            var wnd = new Windows.UnpaidInvoicesWindow(DebtTopN) { Owner = Window.GetWindow(this) };
            wnd.ShowDialog();
        }

        private void gridDebt_MouseDoubleClick(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            if (gridDebt.SelectedItem is SalesReadService.CustomerOutstandingDto row)
            {
                var wnd = new Windows.UnpaidInvoicesWindow(DebtTopN, row.CustomerAccountId) { Owner = Window.GetWindow(this) };
                wnd.ShowDialog();
            }
        }

        private void btnTheme_Click(object sender, RoutedEventArgs e)
        {
            ThemeManager.Toggle();
            ThemeText = UserPrefs.Current.Theme == "Dark" ? "Dark" : "Light";
        }

        // ---------- INotifyPropertyChanged ----------
        public event PropertyChangedEventHandler? PropertyChanged;
        private void OnPropertyChanged([CallerMemberName] string? name = null)
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }
}
