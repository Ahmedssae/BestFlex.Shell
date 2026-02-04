using System.Windows;
using BestFlex.Shell.ViewModels;
using Microsoft.Extensions.DependencyInjection;

namespace BestFlex.Shell.Views
{
    public partial class InventoryOverviewWindow : Window
    {
        public InventoryOverviewWindow()
        {
            InitializeComponent();
            
            // Get ViewModel from DI container
            var app = (App)System.Windows.Application.Current;
            if (app.ServiceProvider != null)
            {
                DataContext = app.ServiceProvider.GetRequiredService<InventoryOverviewViewModel>();
            }
        }
    }
}
