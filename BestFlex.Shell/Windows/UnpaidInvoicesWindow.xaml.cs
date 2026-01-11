using System;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using BestFlex.Infrastructure.Services;
using BestFlex.Persistence.Data;
using Microsoft.Extensions.DependencyInjection;

namespace BestFlex.Shell.Windows
{
    public partial class UnpaidInvoicesWindow : Window
    {
        private readonly ObservableCollection<SalesReadService.CustomerOutstandingDto> _customers = new();
        private readonly ObservableCollection<SalesReadService.InvoiceSummaryDto> _invoices = new();

        private readonly int? _preselectCustomerId;

        public UnpaidInvoicesWindow(int top, int? preselectCustomerId = null)
        {
            InitializeComponent();
            gridCustomers.ItemsSource = _customers;
            gridInvoices.ItemsSource = _invoices;

            _preselectCustomerId = preselectCustomerId;

            // Set Top combo
            var idx = new[] { 5, 10, 20, 50, 100 }.ToList().FindIndex(x => x == top);
            cmbTop.SelectedIndex = idx >= 0 ? idx : 1;

            Loaded += async (_, __) => await ReloadAsync();
        }

        private int TopN
        {
            get
            {
                if (cmbTop.SelectedItem is System.Windows.Controls.ComboBoxItem it
                    && int.TryParse(it.Content?.ToString(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var n))
                    return n;
                return 10;
            }
        }

        private async Task ReloadAsync(CancellationToken ct = default)
        {
            try
            {
                var sp = ((App)System.Windows.Application.Current).Services;
                using var scope = sp.CreateScope();
                var db = scope.ServiceProvider.GetRequiredService<BestFlexDbContext>();
                var svc = new SalesReadService(db);

                var top = await svc.GetTopOutstandingAsync(TopN, ct);

                _customers.Clear();
                foreach (var c in top) _customers.Add(c);
                txtSummary.Text = $"Top {_customers.Count} customers by outstanding amount";

                // preselect
                if (_preselectCustomerId.HasValue)
                {
                    var row = _customers.FirstOrDefault(x => x.CustomerAccountId == _preselectCustomerId.Value);
                    if (row != null) gridCustomers.SelectedItem = row;
                    await LoadInvoicesForSelectedAsync(ct);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, $"Failed to load unpaid invoices.\n\n{ex.Message}", "Unpaid Invoices",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private async Task LoadInvoicesForSelectedAsync(CancellationToken ct = default)
        {
            try
            {
                if (gridCustomers.SelectedItem is not SalesReadService.CustomerOutstandingDto row) return;

                var sp = ((App)System.Windows.Application.Current).Services;
                using var scope = sp.CreateScope();
                var db = scope.ServiceProvider.GetRequiredService<BestFlexDbContext>();
                var svc = new SalesReadService(db);

                var list = await svc.GetInvoicesForCustomerAsync(row.CustomerAccountId, ct);

                _invoices.Clear();
                foreach (var inv in list) _invoices.Add(inv);
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, $"Failed to load customer invoices.\n\n{ex.Message}", "Unpaid Invoices",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private async void OpenStatement_Click(object sender, RoutedEventArgs e)
        {
            var selectedCustomer = gridCustomers.SelectedItem as dynamic;
            if (selectedCustomer == null)
            {
                MessageBox.Show(this, "Select a customer first.", "Unpaid Invoices",
                    MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            string customerName = selectedCustomer.Name; // keep if your DTO has Name
            var app = (App)System.Windows.Application.Current;   // << fix here
            var wnd = app.Services.GetRequiredService<AccountStatementWindow>();
            wnd.Owner = this;

            var from = DateTime.Today.AddDays(-90);
            var to = DateTime.Today;

            await wnd.PreloadAsync(customerName, from, to, includeAging: true);
            wnd.ShowDialog();
        }




        private async void Refresh_Click(object sender, RoutedEventArgs e) => await ReloadAsync();
        private async void gridCustomers_SelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
            => await LoadInvoicesForSelectedAsync();
    }
}
