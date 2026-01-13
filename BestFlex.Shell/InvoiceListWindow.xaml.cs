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

            // Auto-set a wide, useful range and load on window loaded
            Loaded += async (_, __) => await _vm.LoadAsync();
        }

        private async void Search_Click(object sender, RoutedEventArgs e)
            => await _vm.SearchAsync(resetToFirstPage: true);
        private void Go_Click(object sender, RoutedEventArgs e)
        {
            // Handled by command binding in XAML; keep method for legacy hookup if needed.
        }

        // Paging handled via commands on the ViewModel (bound in XAML). Code-behind performs no paging.

        private void GridInvoices_MouseDoubleClick(object sender, MouseButtonEventArgs e)
            => OpenSelected();

        private void Open_Click(object sender, RoutedEventArgs e)
            => OpenSelected();

        private void OpenSelected()
        {
            var row = GridInvoices?.SelectedItem as InvoiceListViewModel.InvoiceRow
                      ?? _vm.Items.FirstOrDefault();

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
