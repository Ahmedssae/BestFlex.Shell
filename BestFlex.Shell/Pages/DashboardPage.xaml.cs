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
        private readonly ViewModels.DashboardViewModel _vm;

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
            _vm = ((App)System.Windows.Application.Current).Services.GetRequiredService<ViewModels.DashboardViewModel>();
            gridLow.ItemsSource = _vm.LowStock;
            gridDebt.ItemsSource = _vm.TopDebt;

            // Set DataContext to the dashboard view model. ThemeText in XAML uses RelativeSource to the UserControl
            // so it remains available from the code-behind even when DataContext is the VM.
            DataContext = _vm;
            // Also bind the low/debt grids to the VM (done above)
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
                await _vm.ReloadLowAsync(LowThreshold, LowTopN, ct);
                txtLowSummary.Text = _vm.LowSummary;
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
                await _vm.ReloadDebtAsync(DebtTopN, ct);
                txtDebtSummary.Text = _vm.DebtSummary;
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
            var from = DateTime.Today.AddDays(-13); // last 14 days by default
            var to = DateTime.Today;
            await _vm.ReloadAllAsync(LowThreshold, LowTopN, DebtTopN, from, to);
            txtLowSummary.Text = _vm.LowSummary;
            txtDebtSummary.Text = _vm.DebtSummary;
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
