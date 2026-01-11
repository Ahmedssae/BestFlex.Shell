using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using BestFlex.Application.Abstractions.Inventory;

namespace BestFlex.Shell.Views.Pages.Inventory
{
    /// <summary>
    /// ViewModel for ReceiveStockPage. Holds lines, total calculation and save logic.
    /// Business logic and data access moved here so the view is UI-only.
    /// </summary>
    public sealed class ReceiveStockPageViewModel
    {
        private readonly IPurchaseReceiveHandler _handler;

        public ObservableCollection<LineVm> Lines { get; } = new();

        public PurchaseReceiptResult? LastResult { get; private set; }

        public ReceiveStockPageViewModel(IPurchaseReceiveHandler handler)
        {
            _handler = handler ?? throw new ArgumentNullException(nameof(handler));
        }

        public void AddBlankLine()
        {
            var vm = new LineVm();
            vm.PropertyChanged += (_, __) => { /* UI listens to changes via binding; totals are computed on demand */ };
            Lines.Add(vm);
        }

        public void RemoveLine(LineVm vm)
        {
            if (vm == null) return;
            Lines.Remove(vm);
        }

        public decimal ComputeTotal()
        {
            return Lines.Sum(l => l.LineTotal);
        }

        /// <summary>
        /// Perform validation, construct a ReceiveDraft and call the handler to persist.
        /// Returns the result and the draft used (for printing).
        /// Throws InvalidOperationException on validation errors.
        /// </summary>
        public async Task<(ReceiveDraft Draft, PurchaseReceiptResult Result)> SaveAsync(string supplier, string documentNumber, DateTime date, string? notes, CancellationToken ct = default)
        {
            if (string.IsNullOrWhiteSpace(supplier))
                throw new InvalidOperationException("Supplier is required.");
            if (string.IsNullOrWhiteSpace(documentNumber))
                throw new InvalidOperationException("Document No is required.");

            var lines = Lines
                .Where(l => !string.IsNullOrWhiteSpace(l.Code) && l.Quantity > 0)
                .Select(l => new ReceiveLine(l.Code!.Trim(), l.Name?.Trim(), l.Quantity, l.UnitCost))
                .ToList();

            if (lines.Count == 0)
                throw new InvalidOperationException("Add at least one valid line.");

            var draft = new ReceiveDraft(
                Supplier: supplier.Trim(),
                DocumentNumber: documentNumber.Trim(),
                Date: date,
                Lines: lines,
                Notes: string.IsNullOrWhiteSpace(notes) ? null : notes.Trim()
            );

            LastResult = await _handler.ReceiveAsync(draft, ct);
            return (draft, LastResult!);
        }

        /// <summary>
        /// Reset the view model to initial state (clear lines and last result).
        /// </summary>
        public void Reset()
        {
            Lines.Clear();
            LastResult = null;
        }

        // Simple Line VM used by view. Kept here so VM owns business model and change notifications.
        public sealed class LineVm : INotifyPropertyChanged
        {
            private string? _code;
            private string? _name;
            private decimal _qty;
            private decimal _unitCost;

            public string? Code { get => _code; set { _code = value; OnChanged(nameof(Code)); OnChanged(nameof(LineTotal)); } }
            public string? Name { get => _name; set { _name = value; OnChanged(nameof(Name)); } }
            public decimal Quantity { get => _qty; set { _qty = value; OnChanged(nameof(Quantity)); OnChanged(nameof(LineTotal)); } }
            public decimal UnitCost { get => _unitCost; set { _unitCost = value; OnChanged(nameof(UnitCost)); OnChanged(nameof(LineTotal)); } }

            public decimal LineTotal => Quantity * UnitCost;

            public event PropertyChangedEventHandler? PropertyChanged;
            private void OnChanged(string n) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(n));
        }
    }
}
