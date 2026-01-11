using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using BestFlex.Persistence.Data;
using Microsoft.EntityFrameworkCore;

namespace BestFlex.Shell.ViewModels
{
    public class InvoiceDetailsViewModel : INotifyPropertyChanged
    {
        private readonly BestFlexDbContext _db;

        public InvoiceDetailsViewModel(BestFlexDbContext db) => _db = db;

        public int InvoiceId { get; private set; }
        public string InvoiceNo { get; private set; } = "";
        public DateTime IssuedAt { get; private set; }
        public string Customer { get; private set; } = "";
        public string Currency { get; private set; } = "USD";
        public string Issuer { get; private set; } = "";
        public string? Description { get; private set; }
        public decimal Total { get; private set; }

        public ObservableCollection<LineRow> Lines { get; } = new();

        public event PropertyChangedEventHandler? PropertyChanged;
        void OnPropertyChanged([CallerMemberName] string? n = null)
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(n));

        public async Task LoadAsync(int invoiceId)
        {
            // Project everything server-side (fast, no heavy Includes needed)
            var data = await _db.SellingInvoices
                .AsNoTracking()
                .Where(i => i.Id == invoiceId)
                .Select(i => new
                {
                    i.Id,
                    i.InvoiceNo,
                    i.IssuedAt,
                    Customer = i.CustomerAccount.Name,
                    i.Currency,
                    i.Issuer,
                    i.Description,
                    Items = i.Items.Select(it => new
                    {
                        it.ProductId,
                        Code = it.Product.Code,
                        Name = it.Product.Name,
                        it.Quantity,
                        it.UnitPrice
                    }).ToList()
                })
                .SingleOrDefaultAsync();

            if (data == null)
                throw new InvalidOperationException($"Invoice {invoiceId} not found.");

            InvoiceId = data.Id;
            InvoiceNo = data.InvoiceNo;
            IssuedAt = data.IssuedAt;
            Customer = data.Customer;
            Currency = data.Currency ?? "USD";
            Issuer = data.Issuer ?? "";
            Description = data.Description;

            Lines.Clear();
            decimal total = 0m;
            foreach (var x in data.Items)
            {
                var lineTotal = x.Quantity * x.UnitPrice;
                total += lineTotal;
                Lines.Add(new LineRow
                {
                    ProductId = x.ProductId,
                    Code = x.Code,
                    Name = x.Name,
                    Qty = x.Quantity,
                    UnitPrice = x.UnitPrice,
                    LineTotal = lineTotal
                });
            }
            Total = total;

            OnPropertyChanged(nameof(InvoiceId));
            OnPropertyChanged(nameof(InvoiceNo));
            OnPropertyChanged(nameof(IssuedAt));
            OnPropertyChanged(nameof(Customer));
            OnPropertyChanged(nameof(Currency));
            OnPropertyChanged(nameof(Issuer));
            OnPropertyChanged(nameof(Description));
            OnPropertyChanged(nameof(Total));
            OnPropertyChanged(nameof(Lines));
        }

        public class LineRow
        {
            public int ProductId { get; set; }
            public string Code { get; set; } = "";
            public string Name { get; set; } = "";
            public decimal Qty { get; set; }
            public decimal UnitPrice { get; set; }
            public decimal LineTotal { get; set; }
        }
    }
}
