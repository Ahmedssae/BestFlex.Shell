using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using BestFlex.Application.Abstractions.Inventory;
using BestFlex.Shell.Printing;
using BestFlex.Shell.Windows;

namespace BestFlex.Shell.Views.Pages.Inventory
{
    public partial class ReceiveStockPage : UserControl
    {
        private readonly IPurchaseReceiveHandler _handler;
        private readonly IGrnPrintEngine _printEngine;

        private readonly ObservableCollection<LineVm> _lines = new();
        private PurchaseReceiptResult? _lastResult;

        public ReceiveStockPage(IPurchaseReceiveHandler handler, IGrnPrintEngine printEngine)
        {
            InitializeComponent();
            _handler = handler;
            _printEngine = printEngine;

            grid.ItemsSource = _lines;

            btnAddRow.Click += (_, __) => AddBlankLine();
            btnNew.Click += (_, __) => ResetForm();
            btnSave.Click += async (_, __) => await SaveAsync();
            btnPrint.Click += (_, __) => PreviewLast();

            dpDate.SelectedDate = DateTime.Today;

            Loaded += (_, __) =>
            {
                var w = Window.GetWindow(this) ?? System.Windows.Application.Current?.MainWindow;
                if (w != null) w.PreviewKeyDown += HandleKeys;
            };

            AddBlankLine();
            UpdateTotals();
        }

        private void HandleKeys(object? sender, KeyEventArgs e)
        {
            if (e.Key == Key.S && (Keyboard.Modifiers & ModifierKeys.Control) == ModifierKeys.Control)
            {
                _ = SaveAsync();
                e.Handled = true;
            }
            else if (e.Key == Key.Insert)
            {
                AddBlankLine();
                e.Handled = true;
            }
            else if (e.Key == Key.N && (Keyboard.Modifiers & ModifierKeys.Control) == ModifierKeys.Control)
            {
                ResetForm();
                e.Handled = true;
            }
        }

        private void AddBlankLine()
        {
            var vm = new LineVm();
            vm.PropertyChanged += (_, __) => UpdateTotals();
            _lines.Add(vm);
            UpdateTotals();
            grid.ScrollIntoView(vm);
        }

        private void RemoveLine_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button { DataContext: LineVm vm })
            {
                _lines.Remove(vm);
                UpdateTotals();
            }
        }

        private void UpdateTotals()
        {
            var total = _lines.Sum(l => l.LineTotal);
            txtGrand.Text = total.ToString("0.###");
            txtSummary.Text = $"{_lines.Count} line(s)";
        }

        private async System.Threading.Tasks.Task SaveAsync()
        {
            if (string.IsNullOrWhiteSpace(txtSupplier.Text))
            {
                MessageBox.Show("Supplier is required.", "BestFlex", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }
            if (string.IsNullOrWhiteSpace(txtDocNo.Text))
            {
                MessageBox.Show("Document No is required.", "BestFlex", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }
            var date = dpDate.SelectedDate ?? DateTime.Today;

            var lines = _lines
                .Where(l => !string.IsNullOrWhiteSpace(l.Code) && l.Quantity > 0)
                .Select(l => new ReceiveLine(l.Code!.Trim(), l.Name?.Trim(), l.Quantity, l.UnitCost))
                .ToList();

            if (lines.Count == 0)
            {
                MessageBox.Show("Add at least one valid line.", "BestFlex", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            var draft = new ReceiveDraft(
                Supplier: txtSupplier.Text.Trim(),
                DocumentNumber: txtDocNo.Text.Trim(),
                Date: date,
                Lines: lines,
                Notes: string.IsNullOrWhiteSpace(txtNotes.Text) ? null : txtNotes.Text.Trim()
            );

            try
            {
                _lastResult = await _handler.ReceiveAsync(draft);
                btnPrint.IsEnabled = true;

                var doc = _printEngine.CreateGrnDocument(draft, _lastResult);
                var owner = Window.GetWindow(this) ?? System.Windows.Application.Current?.MainWindow;
                var wnd = new GrnPreviewWindow { Owner = owner };
                wnd.SetDocument(doc);
                wnd.ShowDialog();

                var keepSupplier = txtSupplier.Text;
                ResetForm();
                txtSupplier.Text = keepSupplier;
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to save GRN.\n{ex.Message}", "BestFlex", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void PreviewLast()
        {
            if (_lastResult == null)
            {
                MessageBox.Show("No GRN to preview yet.", "BestFlex", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            var lines = _lines
                .Where(l => !string.IsNullOrWhiteSpace(l.Code) && l.Quantity > 0)
                .Select(l => new ReceiveLine(l.Code!.Trim(), l.Name?.Trim(), l.Quantity, l.UnitCost))
                .ToList();

            var draft = new ReceiveDraft(
                Supplier: txtSupplier.Text.Trim(),
                DocumentNumber: txtDocNo.Text.Trim(),
                Date: dpDate.SelectedDate ?? DateTime.Today,
                Lines: lines,
                Notes: string.IsNullOrWhiteSpace(txtNotes.Text) ? null : txtNotes.Text.Trim()
            );

            var doc = _printEngine.CreateGrnDocument(draft, _lastResult);
            var owner = Window.GetWindow(this) ?? System.Windows.Application.Current?.MainWindow;
            var wnd = new Windows.GrnPreviewWindow { Owner = owner };
            wnd.SetDocument(doc);
            wnd.ShowDialog();
        }

        // ✅ Missing method (caused CS0103) — restored
        private void ResetForm()
        {
            _lines.Clear();
            _lastResult = null;
            txtDocNo.Text = "";
            txtNotes.Text = "";
            dpDate.SelectedDate = DateTime.Today;
            btnPrint.IsEnabled = false;
            AddBlankLine();
            UpdateTotals();
        }

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
