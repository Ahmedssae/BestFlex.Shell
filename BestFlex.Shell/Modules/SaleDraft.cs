using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;

namespace BestFlex.Shell.Models
{
    public sealed class SaleDraft : INotifyPropertyChanged
    {
        public string InvoiceNumber { get; set; } = "";
        public DateTime InvoiceDate { get; set; } = DateTime.Now;
        public string CustomerName { get; set; } = "";
        public string Currency { get; set; } = "USD";

        public decimal DiscountPercent { get; set; }  // header-level
        public decimal TaxPercent { get; set; }

        private decimal _subtotal;
        public decimal Subtotal { get => _subtotal; set { _subtotal = value; OnPropertyChanged(); } }

        private decimal _grandTotal;
        public decimal GrandTotal { get => _grandTotal; set { _grandTotal = value; OnPropertyChanged(); } }

        public ObservableCollection<SaleDraftLine> Lines { get; } = new();

        public event PropertyChangedEventHandler? PropertyChanged;
        private void OnPropertyChanged([CallerMemberName] string? n = null) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(n));

        public void RecalculateTotals()
        {
            Subtotal = Lines.Sum(l => l.Total);
            var afterHeaderDiscount = Subtotal * (1m - (DiscountPercent / 100m));
            GrandTotal = Math.Round(afterHeaderDiscount * (1m + (TaxPercent / 100m)), 2);
            OnPropertyChanged(nameof(Subtotal));
            OnPropertyChanged(nameof(GrandTotal));
        }
    }

    public sealed class SaleDraftLine : INotifyPropertyChanged
    {
        private int? _productId;
        public int? ProductId { get => _productId; set { _productId = value; OnPropertyChanged(); OnPropertyChanged(nameof(HasProduct)); } }

        public bool HasProduct => _productId.HasValue && _productId.Value > 0;

        private string _code = "";
        public string Code { get => _code; set { _code = value ?? ""; OnPropertyChanged(); } }

        private string _name = "";
        public string Name { get => _name; set { _name = value ?? ""; OnPropertyChanged(); } }

        private decimal _qty;
        public decimal Qty { get => _qty; set { _qty = value; OnPropertyChanged(); OnPropertyChanged(nameof(Total)); } }

        private decimal _price;
        public decimal Price { get => _price; set { _price = value; OnPropertyChanged(); OnPropertyChanged(nameof(Total)); } }

        // per-line percentage discount (0..100)
        private decimal _discountPct;
        public decimal DiscountPct
        {
            get => _discountPct;
            set
            {
                var v = value < 0 ? 0 : (value > 100 ? 100 : value);
                _discountPct = v;
                OnPropertyChanged();
                OnPropertyChanged(nameof(Total));
            }
        }

        public decimal Total
        {
            get
            {
                var gross = Qty * Price;
                var afterDisc = gross * (1m - (DiscountPct / 100m));
                return Math.Round(afterDisc, 2);
            }
        }

        public event PropertyChangedEventHandler? PropertyChanged;
        private void OnPropertyChanged([CallerMemberName] string? n = null) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(n));
    }
}
