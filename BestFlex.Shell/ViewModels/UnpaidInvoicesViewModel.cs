using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using BestFlex.Application.UI;
using BestFlex.Infrastructure.Services;

namespace BestFlex.Shell.ViewModels
{
    public class UnpaidInvoicesViewModel : INotifyPropertyChanged
    {
        private ObservableCollection<UnpaidCustomerVm> _customers = new();
        private ObservableCollection<InvoiceSummaryDto> _invoices = new();

        public ObservableCollection<UnpaidCustomerVm> Items
        {
            get => _customers;
            set => SetProperty(ref _customers, value, nameof(Items));
        }

        public ObservableCollection<InvoiceSummaryDto> Invoices
        {
            get => _invoices;
            set => SetProperty(ref _invoices, value, nameof(Invoices));
        }

        public UnpaidInvoicesViewModel()
        {
            // ERP REQUIREMENT: No fake data - start with empty state
            // Data will be loaded asynchronously from real services
        }

        public Task LoadAsync()
        {
            return Task.CompletedTask;
        }

        public Task LoadInvoicesForCustomerAsync(int customerId)
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

    public class UnpaidCustomerVm
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public decimal Balance { get; set; }
    }
}
