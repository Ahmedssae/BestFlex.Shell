using System.Windows;
using BestFlex.Shell.ViewModels;
using Microsoft.Extensions.DependencyInjection;

namespace BestFlex.Shell.Views
{
    public partial class CustomerEditWindow : Window
    {
        public CustomerEditWindow()
        {
            InitializeComponent();
            
            // Get ViewModel from DI container
            var app = (App)System.Windows.Application.Current;
            if (app.ServiceProvider != null)
            {
                DataContext = app.ServiceProvider.GetRequiredService<CustomerEditViewModel>();
            }
        }
        
        public CustomerEditWindow(int customerId)
        {
            InitializeComponent();
            
            // Get ViewModel from DI container with customer ID
            var app = (App)System.Windows.Application.Current;
            if (app.ServiceProvider != null)
            {
                DataContext = ActivatorUtilities.CreateInstance<CustomerEditViewModel>(app.ServiceProvider, customerId);
            }
        }
    }
}
