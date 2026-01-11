using System;
using System.Linq;
using System.Windows;
using System.Windows.Input;
using Microsoft.Extensions.DependencyInjection;
using BestFlex.Shell.ViewModels;

namespace BestFlex.Shell
{
    public partial class InvoiceListWindow : Window
    {
        private readonly InvoiceListViewModel _vm;

        public InvoiceListWindow()
        {
            InitializeComponent();

            var app = (App)System.Windows.Application.Current;
            _vm = app.Services.GetRequiredService<InvoiceListViewModel>();
            DataContext = _vm;

            // Auto-set a wide, useful range and search on load
            Loaded += async (_, __) =>
            {
                // If your VM uses different names (e.g., From/To), adjust below:
                _vm.FromDate = DateTime.Today.AddDays(-90);
                _vm.ToDate = DateTime.Today.AddDays(1);

                // Show all customers by default (if your VM has this property)
                _vm.SelectedCustomer = null;

                await _vm.SearchAsync(resetToFirstPage: true);
            };
        }

        private async void Search_Click(object sender, RoutedEventArgs e)
            => await _vm.SearchAsync(resetToFirstPage: true);

        private async void Prev_Click(object sender, RoutedEventArgs e)
            => await _vm.GoPrevAsync();

        private async void Next_Click(object sender, RoutedEventArgs e)
            => await _vm.GoNextAsync();

        private async void Go_Click(object sender, RoutedEventArgs e)
            => await _vm.GoToPageAsync();

        private void GridInvoices_MouseDoubleClick(object sender, MouseButtonEventArgs e)
            => OpenSelected();

        private void Open_Click(object sender, RoutedEventArgs e)
            => OpenSelected();

        private void OpenSelected()
        {
            var row = GridInvoices?.SelectedItem as InvoiceListViewModel.InvoiceRow
                      ?? _vm.Invoices.FirstOrDefault();

            if (row == null)
            {
                MessageBox.Show("Select an invoice row first.", "Invoices",
                    MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            if (row.Id <= 0)
            {
                MessageBox.Show("This invoice row has no valid Id.", "Invoices",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            var app = (App)System.Windows.Application.Current;
            var wnd = app.Services.GetRequiredService<InvoiceDetailsWindow>();
            wnd.Owner = this;
            wnd.InvoiceId = row.Id;
            wnd.ShowDialog();
        }

        private void Close_Click(object sender, RoutedEventArgs e) => Close();
    }
}
