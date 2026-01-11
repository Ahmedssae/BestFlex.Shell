using System;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;
using BestFlex.Persistence.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using BestFlex.Shell.Infrastructure;

namespace BestFlex.Shell.Views.Pages.Inventory
{
    public partial class ProductsPage : UserControl
    {
        private readonly ObservableCollection<ProductRow> _rows = new();
        private int _page = 0;
        private int _pageSize = 25;
        private int _total = 0;

        public ProductsPage()
        {
            InitializeComponent();
            grid.ItemsSource = _rows;
        }

        private sealed record ProductRow(
            int Id,
            string Code,
            string Name,
            decimal StockQty
        );

        private static int? ParseIntNullable(string? s)
        {
            if (int.TryParse((s ?? "").Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var n)) return n;
            return null;
        }

        private async Task LoadAsync(CancellationToken ct = default)
        {
            ShowOverlay(true);
            try
            {
                var sp = ((App)System.Windows.Application.Current).Services;
                using var scope = sp.CreateScope();
                var db = scope.ServiceProvider.GetRequiredService<BestFlexDbContext>();

                var q = db.Products.AsNoTracking().Select(p => new { p.Id, p.Code, p.Name, p.StockQty });

                var code = (txtCode.Text ?? "").Trim();
                if (!string.IsNullOrEmpty(code))
                    q = q.Where(p => p.Code.Contains(code));

                var name = (txtName.Text ?? "").Trim();
                if (!string.IsNullOrEmpty(name))
                    q = q.Where(p => p.Name.Contains(name));

                var stockMin = ParseIntNullable(txtStockMin.Text);
                if (stockMin.HasValue) q = q.Where(p => p.StockQty >= stockMin.Value);

                var stockMax = ParseIntNullable(txtStockMax.Text);
                if (stockMax.HasValue) q = q.Where(p => p.StockQty <= stockMax.Value);

                _total = await q.CountAsync(ct);

                var pageRows = await q
                    .OrderBy(p => p.Code)
                    .Skip(_page * _pageSize)
                    .Take(_pageSize)
                    .ToListAsync(ct);

                _rows.Clear();
                foreach (var r in pageRows)
                    _rows.Add(new ProductRow(r.Id, r.Code, r.Name, r.StockQty));

                UpdateRangeText();
            }
            catch (Exception ex)
            {
                var owner = Window.GetWindow(this);
                if (owner != null)
                    MessageBox.Show(owner, $"Failed to load products.\n\n{ex.Message}", "Products",
                        MessageBoxButton.OK, MessageBoxImage.Error);
                else
                    MessageBox.Show($"Failed to load products.\n\n{ex.Message}", "Products",
                        MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                ShowOverlay(false);
            }
        }

        private void UpdateRangeText()
        {
            var from = _total == 0 ? 0 : (_page * _pageSize) + 1;
            var to = Math.Min((_page + 1) * _pageSize, _total);
            txtRange.Text = $"Showing {from}-{to} of {_total}";
        }

        private void ShowOverlay(bool on)
        {
            overlay.Visibility = on ? Visibility.Visible : Visibility.Collapsed;
            overlay.IsHitTestVisible = on;
        }

        // Events
        private async void UserControl_Loaded(object sender, RoutedEventArgs e)
        {
            _pageSize = Math.Max(1, UserPrefs.Current.InvoicePageSize); // reuse persisted page size
            var sizes = new[] { 25, 50, 100 };
            var idx = Array.IndexOf(sizes, _pageSize);
            cmbPageSize.SelectedIndex = idx >= 0 ? idx : 0;
            if (idx < 0) _pageSize = sizes[0];

            await LoadAsync();
        }

        private async void BtnSearch_Click(object sender, RoutedEventArgs e)
        {
            _page = 0;
            await LoadAsync();
        }

        private async void BtnClear_Click(object sender, RoutedEventArgs e)
        {
            txtCode.Text = string.Empty;
            txtName.Text = string.Empty;
            txtStockMin.Text = string.Empty;
            txtStockMax.Text = string.Empty;
            _page = 0;
            await LoadAsync();
        }

        private async void BtnPrev_Click(object sender, RoutedEventArgs e)
        {
            if (_page > 0)
            {
                _page--;
                await LoadAsync();
            }
        }

        private async void BtnNext_Click(object sender, RoutedEventArgs e)
        {
            var maxPage = (_total == 0) ? 0 : (_total - 1) / _pageSize;
            if (_page < maxPage)
            {
                _page++;
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
                _pageSize = sz;
                _page = 0;
                UserPrefs.Current.InvoicePageSize = _pageSize;
                UserPrefs.Save();
                await LoadAsync();
            }
        }

        private void BtnAddProduct_Click(object sender, RoutedEventArgs e)
        {
            var wnd = new BestFlex.Shell.Windows.QuickAddProductWindow
            {
                Owner = Window.GetWindow(this)
            };
            if (wnd.ShowDialog() == true)
            {
                _ = LoadAsync(); // refresh after add
            }
        }

        private void BtnExportCsv_Click(object sender, RoutedEventArgs e)
        {
            var cols = new (string, Func<ProductRow, object?>)[]
            {
                ("Code", r => r.Code),
                ("Name", r => r.Name),
                ("StockQty", r => r.StockQty)
            };
            CsvExporter.Export(_rows, cols, "products.csv");
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

                pd.PrintDocument(((IDocumentPaginatorSource)doc).DocumentPaginator, "Products");
            }
        }

        private FlowDocument BuildDocument()
        {
            var doc = new FlowDocument
            {
                FontFamily = new FontFamily("Segoe UI"),
                FontSize = 12,
                PagePadding = new Thickness(40)
            };

            var title = new Paragraph(new Run("Products"))
            {
                FontSize = 18,
                FontWeight = FontWeights.SemiBold,
                Margin = new Thickness(0, 0, 0, 12)
            };
            doc.Blocks.Add(title);

            var table = new Table();
            doc.Blocks.Add(table);

            var widths = new GridLength[] {
                new GridLength(160), // Code
                new GridLength(1, GridUnitType.Star), // Name
                new GridLength(110)  // Stock
            };
            foreach (var w in widths) table.Columns.Add(new TableColumn { Width = w });

            var head = new TableRowGroup();
            var hr = new TableRow();
            head.Rows.Add(hr);
            table.RowGroups.Add(head);

            void H(string t) => hr.Cells.Add(new TableCell(new Paragraph(new Run(t)))
            {
                FontWeight = FontWeights.SemiBold,
                Padding = new Thickness(4, 2, 4, 2),
                BorderBrush = Brushes.LightGray,
                BorderThickness = new Thickness(0, 0, 0, 1)
            });
            H("Code"); H("Name"); H("Stock");

            var body = new TableRowGroup();
            table.RowGroups.Add(body);

            foreach (var r in _rows)
            {
                var tr = new TableRow();
                body.Rows.Add(tr);

                void C(object? t, TextAlignment align = TextAlignment.Left)
                    => tr.Cells.Add(new TableCell(new Paragraph(new Run(t?.ToString() ?? "")) { TextAlignment = align })
                    { Padding = new Thickness(4, 2, 4, 2) });

                C(r.Code);
                C(r.Name);
                C(string.Format(CultureInfo.InvariantCulture, "{0}", r.StockQty), TextAlignment.Right);
            }

            var total = _rows.Sum(x => x.StockQty);
            var totals = new Paragraph(new Run($"Total stock (listed page): {total:N2}"))
            {
                Margin = new Thickness(0, 8, 0, 0),
                FontWeight = FontWeights.SemiBold
            };
            doc.Blocks.Add(totals);

            return doc;
        }
    }
}
