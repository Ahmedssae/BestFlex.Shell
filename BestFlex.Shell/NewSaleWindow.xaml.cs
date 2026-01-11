using System.Windows;
using Microsoft.Extensions.DependencyInjection;
using BestFlex.Shell.ViewModels;
using BestFlex.Shell.Models;
using BestFlex.Shell.Services;

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

            Loaded += async (_, __) => await _vm.LoadAsync();
        }

        private async void AddLine_Click(object sender, RoutedEventArgs e) => await _vm.AddLineAsync();
        private async void Save_Click(object sender, RoutedEventArgs e) => await _vm.SaveAsync();
        private void Close_Click(object sender, RoutedEventArgs e) => Close();
    }
}
