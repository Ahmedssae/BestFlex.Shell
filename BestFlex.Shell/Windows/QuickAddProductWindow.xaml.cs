using System;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using BestFlex.Domain.Entities;
using BestFlex.Persistence.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace BestFlex.Shell.Windows
{
    public partial class QuickAddProductWindow : Window
    {
        public Product? CreatedProduct { get; private set; }

        public QuickAddProductWindow()
        {
            InitializeComponent();
        }

        private decimal? ParsePrice(string? s)
        {
            if (string.IsNullOrWhiteSpace(s)) return null;
            if (decimal.TryParse(s.Trim(), NumberStyles.Number, CultureInfo.InvariantCulture, out var d)) return d;
            if (decimal.TryParse(s.Trim(), out d)) return d;
            return null;
        }

        private async void Save_Click(object sender, RoutedEventArgs e)
        {
            var code = (txtCode.Text ?? "").Trim();
            var name = (txtName.Text ?? "").Trim();
            var priceOpt = ParsePrice(txtPrice.Text);

            if (string.IsNullOrWhiteSpace(code))
            {
                MessageBox.Show(this, "Code is required.", "Add Product", MessageBoxButton.OK, MessageBoxImage.Warning);
                txtCode.Focus();
                return;
            }
            if (string.IsNullOrWhiteSpace(name))
            {
                MessageBox.Show(this, "Name is required.", "Add Product", MessageBoxButton.OK, MessageBoxImage.Warning);
                txtName.Focus();
                return;
            }

            try
            {
                // ERP REQUIREMENT: Use proper transaction boundaries, not direct DbContext access
                var sp = ((App)System.Windows.Application.Current).Services;
                using var scope = sp.CreateScope();
                
                var unitOfWork = scope.ServiceProvider.GetRequiredService<BestFlex.Application.Abstractions.IUnitOfWork>();
                var productReadService = scope.ServiceProvider.GetRequiredService<BestFlex.Application.Abstractions.IProductReadService>();
                
                // Begin transaction
                await unitOfWork.BeginAsync();

                // Check for existing product using service
                var existingProducts = await productReadService.GetForSalesAsync();
                if (existingProducts.Any(p => p.Code.Equals(code, StringComparison.OrdinalIgnoreCase)))
                {
                    await unitOfWork.RollbackAsync();
                    MessageBox.Show(this, "A product with this Code already exists.", "Add Product",
                        MessageBoxButton.OK, MessageBoxImage.Error);
                    return;
                }

                // TODO: Replace with proper Application Service call when available
                // For now, implement explicit failure as per ERP requirements
                await unitOfWork.RollbackAsync();
                throw new NotImplementedException("Product creation through UI not yet implemented - requires proper Application Service integration");
            }
            catch (NotImplementedException)
            {
                MessageBox.Show(this, "Product creation functionality is not yet available. This feature requires implementation of proper Application Services.", "Feature Not Available",
                    MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, $"Failed to save product.\n\n{ex.Message}", "Add Product",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
    }
}
