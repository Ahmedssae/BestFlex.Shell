using System;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;
using BestFlex.Persistence.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using BestFlex.Shell.Infrastructure;

namespace BestFlex.Shell.Pages
{
    public partial class InvoicesPage : UserControl
    {
        // Use a dedicated ViewModel for data access and paging logic. The code-behind
        // remains responsible for UI wiring (events, print/export) only.
        private readonly InvoicesPageViewModel _vm;

        public InvoicesPage()
        {
            InitializeComponent();
            _vm = new InvoicesPageViewModel(((App)System.Windows.Application.Current).Services);
            grid.ItemsSource = _vm.Rows;
        }

        private async System.Threading.Tasks.Task LoadAsync(System.Threading.CancellationToken ct = default)
        {
            ShowOverlay(true);
            try
            {
                // Copy UI filters into VM
                _vm.NumberFilter = (txtNumber.Text ?? "").Trim();
                _vm.CustomerFilter = (txtCustomer.Text ?? "").Trim();
                _vm.From = dpFrom.SelectedDate?.Date;
                _vm.To = dpTo.SelectedDate?.Date;

                // Page settings
                _vm.Page = _vm.Page; // preserved by UI actions
                _vm.PageSize = _vm.PageSize; // persisted separately via UI

                await _vm.LoadAsync(ct);

                UpdateRangeText();
            }
            catch (Exception ex)
            {
                var owner = Window.GetWindow(this);
                if (owner != null)
                    MessageBox.Show(owner, $"Failed to load invoices.\n\n{ex.Message}", "Invoices", MessageBoxButton.OK, MessageBoxImage.Error);
                else
                    MessageBox.Show($"Failed to load invoices.\n\n{ex.Message}", "Invoices", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                ShowOverlay(false);
            }
        }

        private void UpdateRangeText()
        {
            var total = _vm.Total;
            var from = total == 0 ? 0 : (_vm.Page * _vm.PageSize) + 1;
            var to = Math.Min((_vm.Page + 1) * _vm.PageSize, total);
            txtRange.Text = $"Showing {from}-{to} of {total}";
        }

        private void ShowOverlay(bool on)
        {
            overlay.Visibility = on ? Visibility.Visible : Visibility.Collapsed;
            overlay.IsHitTestVisible = on;
        }

        // ----------------- Events -----------------

        private async void UserControl_Loaded(object sender, RoutedEventArgs e)
        {
            _vm.PageSize = Math.Max(1, UserPrefs.Current.InvoicePageSize);
            var sizes = new[] { 25, 50, 100 };
            var idx = Array.IndexOf(sizes, _vm.PageSize);
            cmbPageSize.SelectedIndex = idx >= 0 ? idx : 0;
            if (idx < 0) _vm.PageSize = sizes[0];

            await LoadAsync();
        }

        private async void BtnSearch_Click(object sender, RoutedEventArgs e)
        {
            _vm.Page = 0;
            await LoadAsync();
        }

        private async void BtnClear_Click(object sender, RoutedEventArgs e)
        {
            txtNumber.Text = string.Empty;
            txtCustomer.Text = string.Empty;
            dpFrom.SelectedDate = null;
            dpTo.SelectedDate = null;
            _vm.Page = 0;
            await LoadAsync();
        }

        private async void BtnPrev_Click(object sender, RoutedEventArgs e)
        {
            if (_vm.Page > 0)
            {
                _vm.Page--;
                await LoadAsync();
            }
        }

        private async void BtnNext_Click(object sender, RoutedEventArgs e)
        {
            var maxPage = (_vm.Total == 0) ? 0 : (_vm.Total - 1) / _vm.PageSize;
            if (_vm.Page < maxPage)
            {
                _vm.Page++;
                await LoadAsync();
            }
        }

        private async void cmbPageSize_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (!IsLoaded) return;

            if (cmbPageSize.SelectedItem is ComboBoxItem it &&
                int.TryParse(it.Content?.ToString(), out var sz) &&
                sz > 0)
            {
                _vm.PageSize = sz;
                _vm.Page = 0;

                UserPrefs.Current.InvoicePageSize = _vm.PageSize;
                UserPrefs.Save();

                await LoadAsync();
            }
        }

        // Export & Print
        private void BtnExportCsv_Click(object sender, RoutedEventArgs e)
        {
            var cols = new (string, Func<InvoiceRow, object?>)[]
            {
                ("InvoiceNo", r => r.InvoiceNo),
                ("IssuedAt",  r => r.IssuedAt.ToString("yyyy-MM-dd HH:mm")),
                ("Customer",  r => r.CustomerName),
                ("Items",     r => r.Items),
                ("Amount",    r => r.Amount),
                ("Currency",  r => r.Currency)
            };
            CsvExporter.Export(_vm.Rows, cols, "invoices.csv");
        }

        private void BtnPrint_Click(object sender, RoutedEventArgs e)
        {
            var doc = BuildDocument();

            var pd = new PrintDialog();
            if (pd.ShowDialog() == true)
            {
                doc.PageHeight = pd.PrintableAreaHeight;
                doc.PageWidth = pd.PrintableAreaWidth;
                doc.PagePadding = new Thickness(50);
                doc.ColumnGap = 0;
                doc.ColumnWidth = pd.PrintableAreaWidth;

                pd.PrintDocument(((IDocumentPaginatorSource)doc).DocumentPaginator, "Invoices");
            }
        }

        private FlowDocument BuildDocument()
        {
            var doc = new FlowDocument
            {
                FontFamily = new System.Windows.Media.FontFamily("Segoe UI"),
                FontSize = 12,
                PagePadding = new Thickness(40)
            };

            var title = new Paragraph(new Run("Invoices"))
            {
                FontSize = 18,
                FontWeight = FontWeights.SemiBold,
                Margin = new Thickness(0, 0, 0, 12)
            };
            doc.Blocks.Add(title);

            var details = new Paragraph(new Run(BuildFilterCaption()))
            {
                Foreground = Brushes.Gray,
                Margin = new Thickness(0, 0, 0, 10)
            };
            doc.Blocks.Add(details);

            var table = new Table();
            doc.Blocks.Add(table);

            var widths = new GridLength[] {
                new GridLength(140), // No
                new GridLength(160), // Date
                new GridLength(1, GridUnitType.Star), // Customer
                new GridLength(70),  // Items
                new GridLength(110), // Amount
                new GridLength(70)   // Cur
            };
            foreach (var w in widths) table.Columns.Add(new TableColumn { Width = w });

            var header = new TableRow();
            var headerGroup = new TableRowGroup();
            headerGroup.Rows.Add(header);
            table.RowGroups.Add(headerGroup);

            void H(string text) => header.Cells.Add(new TableCell(new Paragraph(new Run(text)))
            {
                FontWeight = FontWeights.SemiBold,
                Padding = new Thickness(4, 2, 4, 2),
                BorderBrush = Brushes.LightGray,
                BorderThickness = new Thickness(0, 0, 0, 1)
            });
            H("#"); H("Date"); H("Customer"); H("Lines"); H("Amount"); H("Cur");

            var body = new TableRowGroup();
            table.RowGroups.Add(body);

            foreach (var r in _vm.Rows)
            {
                var tr = new TableRow();
                body.Rows.Add(tr);

                void C(object? t, TextAlignment align = TextAlignment.Left)
                    => tr.Cells.Add(new TableCell(new Paragraph(new Run(t?.ToString() ?? "")) { TextAlignment = align })
                    { Padding = new Thickness(4, 2, 4, 2) });

                C(r.InvoiceNo);
                C(r.IssuedAt.ToString("yyyy-MM-dd HH:mm"));
                C(r.CustomerName);
                C(r.Items, TextAlignment.Right);
                C(string.Format(CultureInfo.InvariantCulture, "{0:N2}", r.Amount), TextAlignment.Right);
                C(r.Currency);
            }

            var tot = _vm.Rows.Sum(x => x.Amount);
            var totalPara = new Paragraph(new Run($"Total (this page): {tot:N2}"))
            {
                Margin = new Thickness(0, 8, 0, 0),
                FontWeight = FontWeights.SemiBold
            };
            doc.Blocks.Add(totalPara);

            return doc;
        }

        private string BuildFilterCaption()
        {
            var p = new System.Collections.Generic.List<string>();
            if (!string.IsNullOrWhiteSpace(txtNumber.Text)) p.Add($"No contains '{txtNumber.Text}'");
            if (!string.IsNullOrWhiteSpace(txtCustomer.Text)) p.Add($"Customer like '{txtCustomer.Text}'");
            if (dpFrom.SelectedDate.HasValue) p.Add($"From {dpFrom.SelectedDate:yyyy-MM-dd}");
            if (dpTo.SelectedDate.HasValue) p.Add($"To {dpTo.SelectedDate:yyyy-MM-dd}");
            return p.Count == 0 ? "All invoices" : string.Join(" · ", p);
        }

        // Quick-Add actions
        private void BtnAddProduct_Click(object sender, RoutedEventArgs e)
        {
            var wnd = new BestFlex.Shell.Windows.QuickAddProductWindow
            {
                Owner = Window.GetWindow(this)
            };
            wnd.ShowDialog();
        }

        private void BtnAddCustomer_Click(object sender, RoutedEventArgs e)
        {
            var wnd = new BestFlex.Shell.Windows.QuickAddCustomerWindow
            {
                Owner = Window.GetWindow(this)
            };
            if (wnd.ShowDialog() == true)
            {
                var created = wnd.CreatedCustomer;
                var nameProp = created?.GetType().GetProperty("Name");
                var name = nameProp?.GetValue(created)?.ToString();
                if (!string.IsNullOrWhiteSpace(name))
                    txtCustomer.Text = name;
            }
        }

        // Preview handlers
        private void BtnPreview_Click(object sender, RoutedEventArgs e) => OpenPreviewForSelected();

        private void grid_MouseDoubleClick(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            if (grid.SelectedItem != null)
                OpenPreviewForSelected();
        }

        private void OpenPreviewForSelected()
        {
            if (grid.SelectedItem is not InvoiceRow row) return;

            var w = new BestFlex.Shell.Windows.InvoicePreviewWindow(row.Id)
            {
                Owner = Window.GetWindow(this)
            };
            w.ShowDialog();
        }
    }
}
