using System;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using Microsoft.Extensions.DependencyInjection;

namespace BestFlex.Shell.Pages
{
    public partial class NewSalePage : UserControl
    {
        private readonly BestFlex.Shell.ViewModels.NewSaleViewModel _vm;

        public NewSalePage()
        {
            InitializeComponent();
            var app = (App)System.Windows.Application.Current;
            var sales = app.Services.GetRequiredService<BestFlex.Application.Abstractions.ISalesService>();
            _vm = new BestFlex.Shell.ViewModels.NewSaleViewModel(app.Services, sales);
            DataContext = _vm;
        }

        private async void UserControl_Loaded(object sender, RoutedEventArgs e)
        {
            cmbCurrency.SelectedIndex = 0;
            dpDate.SelectedDate = DateTime.Today;

            await _vm.LoadLookupsAsync();
            // bind customers in XAML or set ItemsSource here if necessary
            cmbCustomer.ItemsSource = _vm.Customers;

            // set SelectedCustomerId when UI selection changes (nullable-safe)
            cmbCustomer.SelectionChanged += (_, __) => _vm.SelectedCustomerId = cmbCustomer.SelectedValue as int?;

            // start with one line via VM
            _vm.AddLineCommand.Execute(null);
        }

        private void BtnAddProduct_Click(object sender, RoutedEventArgs e)
        {
            var wnd = new BestFlex.Shell.Windows.QuickAddProductWindow { Owner = Window.GetWindow(this) };
            if (wnd.ShowDialog() != true) return;

            _ = _vm.LoadLookupsAsync().ContinueWith(_ =>
            {
                Dispatcher.Invoke(() =>
                {
                    var just = wnd.CreatedProduct;
                    var idProp = just?.GetType().GetProperty("Id");
                    if (idProp == null) return;
                    int id = (int)Convert.ChangeType(idProp.GetValue(just), typeof(int));
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

            _ = _vm.LoadLookupsAsync().ContinueWith(_ =>
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
