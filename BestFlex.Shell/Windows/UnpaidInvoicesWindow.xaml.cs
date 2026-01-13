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
        private readonly ViewModels.UnpaidInvoicesViewModel _vm;
        private readonly int? _preselectCustomerId;

        public UnpaidInvoicesWindow(int top, int? preselectCustomerId = null)
        {
            InitializeComponent();
            _preselectCustomerId = preselectCustomerId;

            // resolve VM from DI and bind to DataContext
            _vm = ((App)System.Windows.Application.Current).Services.GetRequiredService<ViewModels.UnpaidInvoicesViewModel>();
            DataContext = _vm;

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
            await _vm.LoadAsync(TopN, ct);

            // preselect
            if (_preselectCustomerId.HasValue)
            {
                var row = _vm.Items.FirstOrDefault(x => x.CustomerAccountId == _preselectCustomerId.Value);
                if (row != null) gridCustomers.SelectedItem = row;
                await LoadInvoicesForSelectedAsync(ct);
            }
        }

        private async Task LoadInvoicesForSelectedAsync(CancellationToken ct = default)
        {
            if (gridCustomers.SelectedItem is not ViewModels.UnpaidInvoicesViewModel.UnpaidCustomerVm row) return;

            // Delegate to the ViewModel to load invoices for the selected customer
            await _vm.LoadInvoicesForCustomerAsync(row.CustomerAccountId, ct);
            gridInvoices.ItemsSource = _vm.Invoices;
        }

        private async void OpenStatement_Click(object sender, RoutedEventArgs e)
        {
            var selectedCustomer = gridCustomers.SelectedItem as ViewModels.UnpaidInvoicesViewModel.UnpaidCustomerVm;
            if (selectedCustomer == null)
            {
                MessageBox.Show(this, "Select a customer first.", "Unpaid Invoices",
                    MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            string customerName = selectedCustomer.CustomerName;
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
