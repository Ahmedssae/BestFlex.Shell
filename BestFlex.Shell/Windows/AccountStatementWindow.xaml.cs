using System;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using BestFlex.Application.Abstractions.Statements;

namespace BestFlex.Shell.Windows
{
    public partial class AccountStatementWindow : Window
    {
        private readonly ICustomerStatementService _svc;

        public AccountStatementWindow(ICustomerStatementService svc)
        {
            InitializeComponent();
            _svc = svc;

            dpFrom.SelectedDate = DateTime.Today.AddDays(-30);
            dpTo.SelectedDate = DateTime.Today;
        }

        // Allow caller to preload + auto-load
        public async Task PreloadAsync(string customerName, DateTime from, DateTime to, bool includeAging = true)
        {
            txtCustomer.Text = customerName;
            dpFrom.SelectedDate = from;
            dpTo.SelectedDate = to;
            chkAging.IsChecked = includeAging;
            await LoadAsync();
        }

        private async void Load_Click(object sender, RoutedEventArgs e) => await LoadAsync();

        private async Task LoadAsync()
        {
            if (string.IsNullOrWhiteSpace(txtCustomer.Text))
            {
                MessageBox.Show(this, "Please enter a customer name.", "Account Statement",
                    MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            var from = dpFrom.SelectedDate ?? DateTime.Today.AddDays(-30);
            var to = dpTo.SelectedDate ?? DateTime.Today;

            try
            {
                var result = await _svc.GetAsync(new StatementFilter(
                    Customer: txtCustomer.Text.Trim(),
                    From: from,
                    To: to,
                    IncludeAging: chkAging.IsChecked == true
                ));

                grid.ItemsSource = result.Lines;

                txtOpening.Text = result.OpeningBalance.ToString("N2");
                txtClosing.Text = result.ClosingBalance.ToString("N2");

                if (result.Aging is { } a)
                {
                    txtA0.Text = a.A0To30.ToString("N2");
                    txtA1.Text = a.A31To60.ToString("N2");
                    txtA2.Text = a.A61To90.ToString("N2");
                    txtA3.Text = a.AOver90.ToString("N2");

                }
                else
                {
                    txtA0.Text = txtA1.Text = txtA2.Text = txtA3.Text = "-";
                }

            }
            catch (Exception ex)
            {
                MessageBox.Show(this, $"Failed to load statement.\n\n{ex.Message}", "Account Statement",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void grid_SelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
        {

        }
    }
}
