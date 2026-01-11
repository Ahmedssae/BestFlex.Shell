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
                // IMPORTANT: fully-qualify to WPF Application to avoid BestFlex.Application namespace collision
                var sp = ((App)System.Windows.Application.Current).Services;
                using var scope = sp.CreateScope();
                var db = scope.ServiceProvider.GetRequiredService<BestFlexDbContext>();

                // Unique code check
                var exists = await db.Products.AsNoTracking().AnyAsync(p => p.Code == code);
                if (exists)
                {
                    MessageBox.Show(this, "A product with this Code already exists.", "Add Product",
                        MessageBoxButton.OK, MessageBoxImage.Error);
                    return;
                }

                var product = new Product
                {
                    Code = code,
                    Name = name,
                    StockQty = 0,
                    Version = 0
                };

                if (priceOpt.HasValue)
                {
                    var priceProp = typeof(Product).GetProperty("DefaultPrice")
                                  ?? typeof(Product).GetProperty("SellingPrice")
                                  ?? typeof(Product).GetProperty("Price");
                    if (priceProp != null && priceProp.CanWrite && priceProp.PropertyType == typeof(decimal))
                        priceProp.SetValue(product, priceOpt.Value);
                }

                db.Products.Add(product);
                await db.SaveChangesAsync();

                CreatedProduct = product;
                DialogResult = true;
                Close();
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, $"Failed to save product.\n\n{ex.Message}", "Add Product",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
    }
}
