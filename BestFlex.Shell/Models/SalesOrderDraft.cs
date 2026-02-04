using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;

namespace BestFlex.Shell.Models
{
    // PURE, DUMB, LOCAL model - no services, no dependencies
    public class SalesOrderDraft : INotifyPropertyChanged
    {
        public Guid Id { get; } = Guid.NewGuid();
        
        private string _customerName = string.Empty;
        public string CustomerName
        {
            get => _customerName;
            set
            {
                if (_customerName != value)
                {
                    _customerName = value;
                    OnPropertyChanged();
                    OnPropertyChanged(nameof(CanSave));
                }
            }
        }

        public DateTime OrderDate { get; } = DateTime.Today;
        public string Currency { get; } = "USD";

        public ObservableCollection<SalesOrderLineDraft> Lines { get; } = new();

        public decimal Subtotal => Lines.Sum(l => l.LineTotal);
        public decimal Tax => Subtotal * 0.0m; // tax hardcoded to zero for now
        public decimal GrandTotal => Subtotal + Tax;

        public bool CanSave => !string.IsNullOrWhiteSpace(CustomerName) && Lines.Any(l => l.IsValid);

        public event PropertyChangedEventHandler? PropertyChanged;

        protected virtual void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        // Notify when lines change to update totals
        public void NotifyLinesChanged()
        {
            OnPropertyChanged(nameof(Subtotal));
            OnPropertyChanged(nameof(Tax));
            OnPropertyChanged(nameof(GrandTotal));
            OnPropertyChanged(nameof(CanSave));
        }
    }

    // PURE, DUMB, LOCAL line model - no services, no dependencies
    public class SalesOrderLineDraft : INotifyPropertyChanged
    {
        public Guid LineId { get; } = Guid.NewGuid();

        private string _description = string.Empty;
        public string Description
        {
            get => _description;
            set
            {
                if (_description != value)
                {
                    _description = value;
                    OnPropertyChanged();
                    OnPropertyChanged(nameof(IsValid));
                    OnPropertyChanged(nameof(LineTotal));
                }
            }
        }

        private decimal _quantity = 1;
        public decimal Quantity
        {
            get => _quantity;
            set
            {
                if (_quantity != value)
                {
                    _quantity = value;
                    OnPropertyChanged();
                    OnPropertyChanged(nameof(IsValid));
                    OnPropertyChanged(nameof(LineTotal));
                }
            }
        }

        private decimal _unitPrice = 0;
        public decimal UnitPrice
        {
            get => _unitPrice;
            set
            {
                if (_unitPrice != value)
                {
                    _unitPrice = value;
                    OnPropertyChanged();
                    OnPropertyChanged(nameof(IsValid));
                    OnPropertyChanged(nameof(LineTotal));
                }
            }
        }

        public decimal LineTotal => Quantity * UnitPrice;

        public bool IsValid => !string.IsNullOrWhiteSpace(Description) && Quantity > 0 && UnitPrice >= 0;

        public event PropertyChangedEventHandler? PropertyChanged;

        protected virtual void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}
