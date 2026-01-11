using System;
using System.Threading.Tasks;
using System.Windows;

namespace BestFlex.Shell.Windows
{
    public partial class AccountStatementWindow : Window
    {
        private readonly AccountStatementViewModel _vm;

        public AccountStatementWindow(AccountStatementViewModel vm)
        {
            // Keep code-behind limited to UI initialization and wiring only.
            InitializeComponent();

            _vm = vm ?? throw new ArgumentNullException(nameof(vm));
            DataContext = _vm; // bind controls if needed in XAML in future

            // Initialize the UI inputs from sensible defaults provided by the VM
            dpFrom.SelectedDate = _vm.From;
            dpTo.SelectedDate = _vm.To;
            chkAging.IsChecked = _vm.IncludeAging;
        }

        // Allow caller to preload + auto-load. This delegates to the VM which contains business logic.
        public async Task PreloadAsync(string customerName, DateTime from, DateTime to, bool includeAging = true)
        {
            // UI only: update input controls
            txtCustomer.Text = customerName;
            dpFrom.SelectedDate = from;
            dpTo.SelectedDate = to;
            chkAging.IsChecked = includeAging;

            // Set VM inputs and load. The VM performs data access and calculations.
            await _vm.PreloadAsync(customerName, from, to, includeAging);

            // Update UI from VM results (formatting performed at view layer)
            ApplyResultsToUi();
        }

        private async void Load_Click(object sender, RoutedEventArgs e)
        {
            // Validate simple UI input
            if (string.IsNullOrWhiteSpace(txtCustomer.Text))
            {
                MessageBox.Show(this, "Please enter a customer name.", "Account Statement",
                    MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            // Copy inputs into VM and ask it to load data (business logic lives in VM)
            _vm.Customer = txtCustomer.Text.Trim();
            _vm.From = dpFrom.SelectedDate ?? DateTime.Today.AddDays(-30);
            _vm.To = dpTo.SelectedDate ?? DateTime.Today;
            _vm.IncludeAging = chkAging.IsChecked == true;

            try
            {
                await _vm.LoadAsync();
                ApplyResultsToUi();
            }
            catch (Exception ex)
            {
                // UI-level error handling (show message); VM threw for legit issues (e.g., customer not found)
                MessageBox.Show(this, $"Failed to load statement.\n\n{ex.Message}", "Account Statement",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        // Move presentation-only mapping/formatting here so the VM remains testable and focused on data.
        private void ApplyResultsToUi()
        {
            grid.ItemsSource = _vm.Lines;

            txtOpening.Text = _vm.OpeningBalance.ToString("N2");
            txtClosing.Text = _vm.ClosingBalance.ToString("N2");

            if (_vm.Aging is { } a)
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

        private void grid_SelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
        {
            // Intentionally left empty - purely UI selection handling can remain here if needed later.
        }
    }
}
