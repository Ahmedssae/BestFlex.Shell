using System.Windows;
using BestFlex.Shell.ViewModels;
using Microsoft.Extensions.DependencyInjection;

namespace BestFlex.Shell.Views
{
    public partial class ProductEditWindow : Window
    {
        public ProductEditWindow()
        {
            InitializeComponent();
            
            // Get ViewModel from DI container
            var app = (App)System.Windows.Application.Current;
            if (app.ServiceProvider != null)
            {
                DataContext = app.ServiceProvider.GetRequiredService<ProductEditViewModel>();
            }
        }
        
        public ProductEditWindow(int productId)
        {
            InitializeComponent();
            
            // Get ViewModel from DI container with product ID
            var app = (App)System.Windows.Application.Current;
            if (app.ServiceProvider != null)
            {
                DataContext = ActivatorUtilities.CreateInstance<ProductEditViewModel>(app.ServiceProvider, productId);
            }
        }
    }
}
