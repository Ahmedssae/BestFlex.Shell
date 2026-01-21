using System.Windows;
using Microsoft.Extensions.DependencyInjection;
using BestFlex.Shell.ViewModels;

namespace BestFlex.Shell
{
    public partial class NewSaleWindow : Window
    {
        private readonly NewSaleViewModel _vm;

        public NewSaleWindow()
        {
            InitializeComponent();

            var app = (App)System.Windows.Application.Current;
            _vm = app.Services.GetRequiredService<NewSaleViewModel>();
            DataContext = _vm;

            Loaded += async (_, __) => await _vm.InitializeAsync();
        }

        private void Close_Click(object sender, RoutedEventArgs e) => Close();
    }
}
