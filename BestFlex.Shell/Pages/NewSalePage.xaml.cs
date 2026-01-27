using System;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using BestFlex.Shell.ViewModels;
using BestFlex.Shell.Views;
using BestFlex.Shell.Abstractions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace BestFlex.Shell.Pages
{
    public partial class NewSalePage : UserControl
    {
        private readonly BestFlex.Shell.ViewModels.NewSaleViewModel _vm;

        public NewSalePage(BestFlex.Shell.ViewModels.NewSaleViewModel vm)
        {
            InitializeComponent();
            _vm = vm ?? throw new ArgumentNullException(nameof(vm));
            DataContext = _vm;
        }

        private async void UserControl_Loaded(object sender, RoutedEventArgs e)
        {
            try
            {
                cmbCurrency.SelectedIndex = 0;
                dpDate.SelectedDate = DateTime.Today;

                await _vm.InitializeAsync(); // Use InitializeAsync instead of LoadAsync
                // bind customers in XAML or set ItemsSource here if necessary
                cmbCustomer.ItemsSource = _vm.Customers;

                // set SelectedCustomerId when UI selection changes (nullable-safe)
                cmbCustomer.SelectionChanged += (_, __) => _vm.SelectedCustomerId = cmbCustomer.SelectedValue as int?;

                // start with one line via VM
                _vm.AddLineCommand.Execute(null);
            }
            catch (Exception ex)
            {
                // Navigate to SafeFallbackView on initialization failure
                var serviceProvider = ((App)System.Windows.Application.Current).Services;
                var navigationService = serviceProvider.GetRequiredService<IShellNavigationService>();
                var fallbackVm = new SafeFallbackViewModel(
                    serviceProvider.GetRequiredService<ILogger<SafeFallbackViewModel>>(),
                    serviceProvider,
                    navigationService,
                    $"Failed to initialize New Sale: {ex.Message}");
                
                var fallbackView = new SafeFallbackView();
                fallbackView.DataContext = fallbackVm;
                
                // Replace current content with fallback view
                var parent = this.Parent as ContentControl;
                if (parent != null)
                {
                    parent.Content = fallbackView;
                }
            }
        }

        private void BtnAddProduct_Click(object sender, RoutedEventArgs e)
        {
            var wnd = new BestFlex.Shell.Windows.QuickAddProductWindow { Owner = Window.GetWindow(this) };
            if (wnd.ShowDialog() != true) return;

            _ = _vm.InitializeAsync().ContinueWith(_ =>
            {
                Dispatcher.Invoke(() =>
                {
                    var just = wnd.CreatedProduct;
                    var idProp = just?.GetType().GetProperty("Id");
                    if (idProp == null) return;
                    var idValue = idProp.GetValue(just);
                    int id = idValue != null ? (int)Convert.ChangeType(idValue, typeof(int)) : 0;
                    if (!_vm.Lines.Any()) _vm.AddLineCommand.Execute(null);
                    var line = _vm.Lines.Last();
                    line.ProductId = id;
                    line.UnitPrice = _vm.Products.FirstOrDefault(x => x.Id == id)?.DefaultPrice ?? 0m;
                    line.Quantity = 1m;
                });
            });
        }

        private void BtnAddCustomer_Click(object sender, RoutedEventArgs e)
        {
            var wnd = new BestFlex.Shell.Windows.QuickAddCustomerWindow { Owner = Window.GetWindow(this) };
            if (wnd.ShowDialog() != true) return;

            var created = wnd.CreatedCustomer;
            var nameProp = created?.GetType().GetProperty("Name");
            var name = nameProp?.GetValue(created)?.ToString();
            if (string.IsNullOrWhiteSpace(name)) return;

            _ = _vm.InitializeAsync().ContinueWith(_ =>
            {
                Dispatcher.Invoke(() =>
                {
                    var found = _vm.Customers.FirstOrDefault(x => x.Name == name);
                    if (found != null) cmbCustomer.SelectedValue = found.Id;
                });
            });
        }
    }
}
