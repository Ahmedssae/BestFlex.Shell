using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Globalization;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using BestFlex.Persistence.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace BestFlex.Shell.Pages
{
    public partial class NewSalePage : UserControl, INotifyPropertyChanged
    {
        private readonly BestFlex.Shell.ViewModels.NewSaleViewModel _vm;

        public event PropertyChangedEventHandler? PropertyChanged;

        public NewSalePage()
        {
            InitializeComponent();
            var app = (App)System.Windows.Application.Current;
            var sales = app.Services.GetRequiredService<BestFlex.Application.Abstractions.ISalesService>();
            _vm = new BestFlex.Shell.ViewModels.NewSaleViewModel(app.Services, sales);
            DataContext = _vm;
        }

        private void Raise([CallerMemberName] string? p = null) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(p));

        private async void UserControl_Loaded(object sender, RoutedEventArgs e)
        {
            cmbCurrency.SelectedIndex = 0;
            dpDate.SelectedDate = DateTime.Today;

            await _vm.LoadLookupsAsync();
            // start with one line
            await _vm.AddLineAsync();
            UpdateTotals();
        }

        private async Task LoadLookupsAsync()
        {
            try
            {
                ShowOverlay(true);
                await _vm.LoadLookupsAsync();
                cmbCustomer.ItemsSource = _vm.Customers;
            }
            catch (Exception ex)
            {
                MessageBox.Show(Window.GetWindow(this)!, $"Failed to load lookups.\n\n{ex.Message}", "New Sale",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                ShowOverlay(false);
            }
        }

        // Price resolution moved into NewSaleViewModel.TryResolvePriceAsync

        // ---------- UI actions ----------
        private void BtnAddProduct_Click(object sender, RoutedEventArgs e)
        {
            var wnd = new BestFlex.Shell.Windows.QuickAddProductWindow
            {
                Owner = Window.GetWindow(this)
            };
            if (wnd.ShowDialog() == true)
            {
                // Refresh product list and preselect
                _ = LoadLookupsAsync().ContinueWith(_ =>
                {
                    Dispatcher.Invoke(() =>
                    {
                        var just = wnd.CreatedProduct;
                        var idProp = just?.GetType().GetProperty("Id");
                        if (idProp != null)
                        {
                            int v = (int)Convert.ChangeType(idProp.GetValue(just), typeof(int), CultureInfo.InvariantCulture);
                            var id = v;
                            var line = _vm.Lines.LastOrDefault();
                            if (line == null)
                            {
                                // sync add a line
                                _ = _vm.AddLineAsync();
                                line = _vm.Lines.LastOrDefault();
                            }
                            if (line != null)
                            {
                                line.ProductId = id;
                                line.UnitPrice = _vm.Products.FirstOrDefault(x => x.Id == id)?.DefaultPrice ?? 0m;
                                line.Quantity = 1m;
                                UpdateTotals();
                            }
                        }
                    });
                });
            }
        }

        private void BtnAddCustomer_Click(object sender, RoutedEventArgs e)
        {
            var wnd = new BestFlex.Shell.Windows.QuickAddCustomerWindow
            {
                Owner = Window.GetWindow(this)
            };
            if (wnd.ShowDialog() == true)
            {
                // Prefill customer
                var created = wnd.CreatedCustomer;
                var nameProp = created?.GetType().GetProperty("Name");
                var name = nameProp?.GetValue(created)?.ToString();
                if (!string.IsNullOrWhiteSpace(name))
                {
                    // reload list and select
                    _ = LoadLookupsAsync().ContinueWith(_ =>
                    {
                        Dispatcher.Invoke(() =>
                        {
                            var found = _vm.Customers.FirstOrDefault(x => x.Name == name);
                            if (found != null)
                                cmbCustomer.SelectedValue = found.Id;
                        });
                    });
                }
            }
        }

        private void BtnAddLine_Click(object sender, RoutedEventArgs e) => _ = _vm.AddLineAsync();

        private void BtnRemoveLine_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button b && b.DataContext is BestFlex.Shell.ViewModels.SaleLineVm vm)
            {
                _vm.RemoveLine(vm);
                UpdateTotals();
            }
        }

        private void UpdateTotals()
        {
            var subtotal = _vm.Subtotal;
            txtSubTotal.Text = subtotal.ToString("N2", CultureInfo.InvariantCulture);
            txtItems.Text = _vm.ItemsCount.ToString("N2", CultureInfo.InvariantCulture);
        }

        internal void OnLineChanged()
        {
            UpdateTotals();
        }

        private void ShowOverlay(bool on)
        {
            overlay.Visibility = on ? Visibility.Visible : Visibility.Collapsed;
            overlay.IsHitTestVisible = on;
        }

        // Save button is a placeholder to keep compatibility with your pipeline.
        // Wire this into your existing save handler if needed.
        private void BtnSave_Click(object sender, RoutedEventArgs e)
        {
            MessageBox.Show(Window.GetWindow(this)!,
                "UI is ready. Hook this page's Lines + selected Customer/Currency/Date into your existing save service.",
                "New Sale", MessageBoxButton.OK, MessageBoxImage.Information);
        }
    }

    // View model types live in BestFlex.Shell.ViewModels.NewSaleViewModel; code-behind is UI-only now.
}
