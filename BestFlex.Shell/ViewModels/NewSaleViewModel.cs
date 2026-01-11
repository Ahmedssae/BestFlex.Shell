using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using BestFlex.Application.Abstractions;
using BestFlex.Application.Contracts.Sales;


namespace BestFlex.Shell.ViewModels
{
    public sealed class NewSaleViewModel : ViewModelBase
    {
        private readonly ISalesService _sales;

        public NewSaleViewModel(ISalesService sales)
        {
            _sales = sales;
            SaveCommand = new AsyncRelayCommand(SaveAsync, CanSave);
        }

        public int? CustomerId { get; set; }
        public DateTime InvoiceDate { get; set; } = DateTime.Now;
        public string Currency { get; set; } = "USD";
        public string? Notes { get; set; }

        public ObservableCollection<SaleLineVm> Lines { get; } = new();

        public ICommand SaveCommand { get; }

        private bool CanSave() => Lines.Any() && Lines.All(l => l.ProductId > 0 && l.Quantity > 0 && l.UnitPrice >= 0);

        private async Task SaveAsync()
        {
            try
            {
                var dto = new NewSaleDto
                {
                    CustomerId = CustomerId,
                    InvoiceDate = InvoiceDate,
                    Currency = Currency,
                    Notes = Notes,
                    Items = Lines.Select(l => new NewSaleItemDto
                    {
                        ProductId = l.ProductId,
                        Quantity = l.Quantity,
                        UnitPrice = l.UnitPrice
                    }).ToList()
                };

                var invoiceId = await _sales.CreateSaleAsync(dto);

                MessageBox.Show($"Sale saved. Invoice #{invoiceId}", "BestFlex", MessageBoxButton.OK, MessageBoxImage.Information);

                // Reset for next sale (or navigate to Invoice Details)
                Lines.Clear();
                Notes = null;
                OnPropertyChanged(nameof(Notes));
            }
            catch (InvalidOperationException ex)
            {
                MessageBox.Show(ex.Message, "Couldn’t save", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
            catch (Exception ex)
            {
                MessageBox.Show("Unexpected error:\n" + ex.Message, "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
    }

    public sealed class SaleLineVm : ViewModelBase
    {
        public int ProductId { get; set; }
        public string ProductName { get; set; } = "";
        public decimal Quantity { get; set; }
        public decimal UnitPrice { get; set; }
    }
}
