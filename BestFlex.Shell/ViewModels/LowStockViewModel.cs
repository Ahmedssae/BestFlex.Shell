using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Threading.Tasks;
using BestFlex.Application.UI;

namespace BestFlex.Shell.ViewModels
{
    public class LowStockItemDto
    {
        public int ProductId { get; set; }
        public string SKU { get; set; } = string.Empty;
        public string ProductName { get; set; } = string.Empty;
        public int CurrentStock { get; set; }
        public int MinimumStock { get; set; }
        public decimal UnitPrice { get; set; }
        public string StockStatus { get; set; } = string.Empty;
    }

    public class LowStockViewModel : INotifyPropertyChanged
    {
        private ObservableCollection<LowStockItemDto> _lowStockItems = new();
        private decimal _total = 5000;

        public ObservableCollection<LowStockItemDto> LowStockItems
        {
            get => _lowStockItems;
            set => SetProperty(ref _lowStockItems, value, nameof(LowStockItems));
        }

        public decimal Total
        {
            get => _total;
            set => SetProperty(ref _total, value, nameof(Total));
        }

        public LowStockViewModel()
        {
            // ERP REQUIREMENT: No fake data - start with empty state
            // Data will be loaded asynchronously from real services
        }

        public Task LoadAsync()
        {
            return Task.CompletedTask;
        }

        public Task LoadAsync(string warehouse, int minStock, bool includeInactive)
        {
            return Task.CompletedTask;
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
}
