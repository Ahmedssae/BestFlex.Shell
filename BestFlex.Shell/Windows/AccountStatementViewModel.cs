using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using BestFlex.Application.Abstractions.Statements;

namespace BestFlex.Shell.Windows
{
    /// <summary>
    /// ViewModel for AccountStatementWindow. Contains all business logic / data access
    /// and exposes simple properties the view can consume. Kept lightweight and
    /// focused on transforming service results into view-friendly properties.
    /// </summary>
    public sealed class AccountStatementViewModel : INotifyPropertyChanged
    {
        private readonly ICustomerStatementService _svc;

        public AccountStatementViewModel(ICustomerStatementService svc)
        {
            _svc = svc ?? throw new ArgumentNullException(nameof(svc));
            // sensible defaults
            From = DateTime.Today.AddDays(-30);
            To = DateTime.Today;
            IncludeAging = true;
            Lines = Array.Empty<StatementLine>();
        }

        // --- INotifyPropertyChanged ---
        public event PropertyChangedEventHandler? PropertyChanged;
        private void Raise([CallerMemberName] string? p = null) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(p));

        // --- Bindable properties ---
        public string Customer { get; set; } = string.Empty;
        public DateTime From { get; set; }
        public DateTime To { get; set; }
        public bool IncludeAging { get; set; }

        private IReadOnlyList<StatementLine> _lines = Array.Empty<StatementLine>();
        public IReadOnlyList<StatementLine> Lines
        {
            get => _lines;
            private set { _lines = value; Raise(nameof(Lines)); }
        }

        public decimal OpeningBalance { get; private set; }
        public decimal ClosingBalance { get; private set; }
        public AgingBuckets? Aging { get; private set; }

        /// <summary>
        /// Load statement from service and populate view model properties.
        /// This method contains the former business logic/data access.
        /// Throws exceptions to the caller for UI-level handling (messages).
        /// </summary>
        public async Task LoadAsync()
        {
            var filter = new StatementFilter(Customer?.Trim() ?? string.Empty, From, To, IncludeAging);
            var result = await _svc.GetAsync(filter);

            // Populate simple properties
            Lines = result.Lines;
            OpeningBalance = result.OpeningBalance;
            ClosingBalance = result.ClosingBalance;
            Aging = result.Aging;

            // Notify callers that numeric properties changed (UI code will read and format)
            Raise(nameof(OpeningBalance));
            Raise(nameof(ClosingBalance));
            Raise(nameof(Aging));
        }

        /// <summary>
        /// Convenience helper to set inputs and load in one call.
        /// This is still considered view-model logic because it prepares and triggers data access.
        /// </summary>
        public async Task PreloadAsync(string customer, DateTime from, DateTime to, bool includeAging = true)
        {
            Customer = customer ?? string.Empty;
            From = from;
            To = to;
            IncludeAging = includeAging;
            await LoadAsync();
        }
    }
}
