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

            Loaded += (_, __) => 
            {
                // NO InitializeAsync - new ViewModel starts with explicit state, no background loading
            };
            
            // Dispose ViewModel when window is closed to prevent memory leaks
            Closed += (_, _) => (_vm as IDisposable)?.Dispose();
        }

        private void Close_Click(object sender, RoutedEventArgs e) => Close();
    }
}
