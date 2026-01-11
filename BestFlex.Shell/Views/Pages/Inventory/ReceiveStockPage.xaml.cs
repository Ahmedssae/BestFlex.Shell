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
        private readonly ReceiveStockPageViewModel _vm;
        private readonly IGrnPrintEngine _printEngine;

        public ReceiveStockPage(IPurchaseReceiveHandler handler, IGrnPrintEngine printEngine)
        {
            InitializeComponent();
            _vm = new ReceiveStockPageViewModel(handler);
            _printEngine = printEngine;

            grid.ItemsSource = _vm.Lines;

            // UI-only event wiring
            btnAddRow.Click += (_, __) => { _vm.AddBlankLine(); UpdateTotals(); };
            btnNew.Click += (_, __) => ResetForm();
            btnSave.Click += async (_, __) => await SaveAsync();
            btnPrint.Click += (_, __) => PreviewLast();

            dpDate.SelectedDate = DateTime.Today;

            Loaded += (_, __) =>
            {
                var w = Window.GetWindow(this) ?? System.Windows.Application.Current?.MainWindow;
                if (w != null) w.PreviewKeyDown += HandleKeys;
            };

            _vm.AddBlankLine();
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
            _vm.AddBlankLine();
            UpdateTotals();
            grid.ScrollIntoView(_vm.Lines.LastOrDefault());
        }

        private void RemoveLine_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button { DataContext: ReceiveStockPageViewModel.LineVm vm })
            {
                _vm.RemoveLine(vm);
                UpdateTotals();
            }
        }

        private void UpdateTotals()
        {
            var total = _vm.ComputeTotal();
            txtGrand.Text = total.ToString("0.###");
            txtSummary.Text = $"{_vm.Lines.Count} line(s)";
        }

        private async System.Threading.Tasks.Task SaveAsync()
        {
            try
            {
                var date = dpDate.SelectedDate ?? DateTime.Today;
                var (draft, result) = await _vm.SaveAsync(txtSupplier.Text, txtDocNo.Text, date, txtNotes.Text);

                btnPrint.IsEnabled = true;

                var doc = _printEngine.CreateGrnDocument(draft, result);
                var owner = Window.GetWindow(this) ?? System.Windows.Application.Current?.MainWindow;
                var wnd = new GrnPreviewWindow { Owner = owner };
                wnd.SetDocument(doc);
                wnd.ShowDialog();

                var keepSupplier = txtSupplier.Text;
                ResetForm();
                txtSupplier.Text = keepSupplier;
            }
            catch (InvalidOperationException ex)
            {
                MessageBox.Show(ex.Message, "BestFlex", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to save GRN.\n{ex.Message}", "BestFlex", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void PreviewLast()
        {
            if (_vm.LastResult == null)
            {
                MessageBox.Show("No GRN to preview yet.", "BestFlex", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            // Build a draft from current UI values for preview (presentation only)
            var lines = _vm.Lines
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

            var doc = _printEngine.CreateGrnDocument(draft, _vm.LastResult);
            var owner = Window.GetWindow(this) ?? System.Windows.Application.Current?.MainWindow;
            var wnd = new Windows.GrnPreviewWindow { Owner = owner };
            wnd.SetDocument(doc);
            wnd.ShowDialog();
        }

        // ✅ Missing method (caused CS0103) — restored
        private void ResetForm()
        {
            _vm.Reset();
            txtDocNo.Text = "";
            txtNotes.Text = "";
            dpDate.SelectedDate = DateTime.Today;
            btnPrint.IsEnabled = false;
            AddBlankLine();
            UpdateTotals();
        }
        // LineVm is provided by ReceiveStockPageViewModel.LineVm
    }
}
