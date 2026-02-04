using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Threading.Tasks;

namespace BestFlex.Shell.ViewModels
{
    public class InvoiceDetailsViewModel : INotifyPropertyChanged
    {
        private string _invoiceNumber = "INV-001";
        private decimal _total = 1000;
        private string _currency = "USD";
        private ObservableCollection<InvoiceLineViewModel> _lines = new();
        private CustomerViewModel _customer = new();
        private DateTime _issuedAt = DateTime.Now;

        public string InvoiceNumber
        {
            get => _invoiceNumber;
            set => SetProperty(ref _invoiceNumber, value, nameof(InvoiceNumber));
        }

        public string InvoiceNo => InvoiceNumber;

        public decimal Total
        {
            get => _total;
            set => SetProperty(ref _total, value, nameof(Total));
        }

        public string Currency
        {
            get => _currency;
            set => SetProperty(ref _currency, value, nameof(Currency));
        }

        public ObservableCollection<InvoiceLineViewModel> Lines
        {
            get => _lines;
            set => SetProperty(ref _lines, value, nameof(Lines));
        }

        public CustomerViewModel Customer
        {
            get => _customer;
            set => SetProperty(ref _customer, value, nameof(Customer));
        }

        public DateTime IssuedAt
        {
            get => _issuedAt;
            set => SetProperty(ref _issuedAt, value, nameof(IssuedAt));
        }

        public InvoiceDetailsViewModel()
        {
            Lines.Add(new InvoiceLineViewModel { Description = "Item 1", Quantity = 1, Price = 500 });
            Lines.Add(new InvoiceLineViewModel { Description = "Item 2", Quantity = 2, Price = 250 });
        }

        public Task LoadAsync()
        {
            return Task.CompletedTask;
        }

        public Task<BestFlex.Application.Abstractions.InvoicePrintData> PrepareInvoicePrintData()
        {
            var data = new BestFlex.Application.Abstractions.InvoicePrintData
            {
                InvoiceNo = InvoiceNo,
                IssuedAt = IssuedAt,
                Currency = Currency,
                CustomerName = Customer.Name,
                Total = Total
            };
            
            // Add lines to the existing collection
            foreach (var line in Lines.Select(l => new BestFlex.Application.Abstractions.InvoicePrintLine
            {
                Code = l.Code,
                Name = l.Name,
                Qty = l.Qty,
                UnitPrice = l.UnitPrice,
                LineTotal = l.LineTotal
            }))
            {
                data.Lines.Add(line);
            }
            
            return Task.FromResult(data);
        }

        public event PropertyChangedEventHandler? PropertyChanged;

        protected virtual void OnPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        protected bool SetProperty<T>(ref T field, T value, string propertyName)
        {
            if (Equals(field, value)) return false;
            field = value;
            OnPropertyChanged(propertyName);
            return true;
        }
    }

    public class InvoiceLineViewModel
    {
        public string Description { get; set; } = string.Empty;
        public string Code { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public int Qty { get; set; }
        public decimal Quantity { get; set; }
        public decimal Price { get; set; }
        public decimal UnitPrice { get; set; }
        public decimal LineTotal { get; set; }
    }

    public class CustomerViewModel
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public string Phone { get; set; } = string.Empty;
        public decimal CreditLimit { get; set; }
        public decimal CurrentBalance { get; set; }
        public bool IsActive { get; set; } = true;
        public DateTime CreatedAt { get; set; }
        public DateTime? IssuedAt { get; set; } // Made nullable since it might not always be set
    }
}
