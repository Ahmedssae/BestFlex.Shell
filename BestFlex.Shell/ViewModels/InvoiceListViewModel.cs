using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using BestFlex.Application.UI;

namespace BestFlex.Shell.ViewModels
{
    public class InvoiceListViewModel : INotifyPropertyChanged
    {
        private ObservableCollection<InvoiceSummaryDto> _invoices = new();

        public ObservableCollection<InvoiceSummaryDto> Invoices
        {
            get => _invoices;
            set => SetProperty(ref _invoices, value, nameof(Invoices));
        }

        public ObservableCollection<InvoiceRow> Items { get; set; } = new();

        public InvoiceListViewModel()
        {
            // ERP REQUIREMENT: No fake data - start with empty state
            // Data will be loaded asynchronously from real services
        }

        public Task LoadAsync()
        {
            return Task.CompletedTask;
        }

        public Task SearchAsync(string searchTerm)
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

    public sealed record InvoiceRow(
        int Id,
        string InvoiceNo,
        DateTime IssuedAt,
        string CustomerName,
        int Items,
        decimal Amount,
        string Currency
    );
}
