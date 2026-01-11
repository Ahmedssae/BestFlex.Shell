using System;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;          // ✅ for PrintDialog
using System.Windows.Documents;
using System.Windows.Media;
using BestFlex.Persistence.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace BestFlex.Shell.Windows
{
    public partial class InvoicePreviewWindow : Window
    {
        private readonly int _invoiceId;
        private FlowDocument? _doc;

        public InvoicePreviewWindow(int invoiceId)
        {
            _invoiceId = invoiceId;
            InitializeComponent();
            Loaded += async (_, __) => await LoadAsync();
        }

        private async Task LoadAsync()
        {
            try
            {
                var sp = ((App)System.Windows.Application.Current).Services;
                using var scope = sp.CreateScope();
                var db = scope.ServiceProvider.GetRequiredService<BestFlexDbContext>();

                // Header
                var head = await (
                    from inv in db.SellingInvoices.AsNoTracking()
                    join ca in db.CustomerAccounts.AsNoTracking() on inv.CustomerAccountId equals ca.Id
                    where inv.Id == _invoiceId
                    select new
                    {
                        inv.Id,
                        inv.InvoiceNo,
                        inv.IssuedAt,
                        inv.Currency,
                        Customer = ca.Name,
                        inv.Description
                    }).SingleAsync();

                // Lines: join to Products to get Code/Name (no ProductName on item)
                var linesDb = await (
                    from i in db.SellingInvoiceItems.AsNoTracking()
                    where i.SellingInvoiceId == _invoiceId
                    join p in db.Products.AsNoTracking() on i.ProductId equals p.Id into gp
                    from p in gp.DefaultIfEmpty()
                    select new
                    {
                        i.ProductId,
                        Code = p.Code,      // may be null if product was deleted
                        Name = p.Name,      // may be null if product was deleted
                        i.Quantity,
                        i.UnitPrice
                    })
                    .ToListAsync();

                var total = linesDb.Aggregate(0m, (sum, l) => sum + (l.Quantity * l.UnitPrice));

                txtHeader.Text = $"{head.InvoiceNo}  —  {head.IssuedAt:yyyy-MM-dd HH:mm}";

                _doc = new FlowDocument
                {
                    FontFamily = new FontFamily("Segoe UI"),
                    FontSize = 12,
                    PagePadding = new Thickness(40)
                };

                var title = new Paragraph(new Run("INVOICE"))
                {
                    FontSize = 20,
                    FontWeight = FontWeights.SemiBold,
                    Margin = new Thickness(0, 0, 0, 6)
                };
                _doc.Blocks.Add(title);

                _doc.Blocks.Add(new Paragraph(new Run(
                    $"No: {head.InvoiceNo}   •   Date: {head.IssuedAt:yyyy-MM-dd HH:mm}   •   Customer: {head.Customer}"))
                { Foreground = Brushes.Gray, Margin = new Thickness(0, 0, 0, 12) });

                // Table
                var table = new Table();
                _doc.Blocks.Add(table);

                var widths = new GridLength[] {
                    new GridLength(110),                     // Code
                    new GridLength(1, GridUnitType.Star),    // Product
                    new GridLength(90),                      // Qty
                    new GridLength(100),                     // Unit
                    new GridLength(110)                      // Line Total
                };
                foreach (var w in widths) table.Columns.Add(new TableColumn { Width = w });

                var headGroup = new TableRowGroup();
                var hr = new TableRow();
                headGroup.Rows.Add(hr);
                table.RowGroups.Add(headGroup);

                void H(string t) => hr.Cells.Add(new TableCell(new Paragraph(new Run(t)))
                {
                    FontWeight = FontWeights.SemiBold,
                    Padding = new Thickness(4, 2, 4, 2),
                    BorderBrush = Brushes.LightGray,
                    BorderThickness = new Thickness(0, 0, 0, 1)
                });

                H("Code"); H("Product"); H("Qty"); H("Unit Price"); H("Line Total");

                var body = new TableRowGroup();
                table.RowGroups.Add(body);

                foreach (var l in linesDb)
                {
                    var tr = new TableRow();
                    body.Rows.Add(tr);

                    void C(object? t, TextAlignment align = TextAlignment.Left)
                        => tr.Cells.Add(new TableCell(new Paragraph(new Run(t?.ToString() ?? "")) { TextAlignment = align })
                        { Padding = new Thickness(4, 2, 4, 2) });

                    // Fallbacks if product was removed
                    var code = string.IsNullOrWhiteSpace(l.Code) ? l.ProductId.ToString() : l.Code;
                    var name = l.Name ?? "(product)";

                    C(code);
                    C(name);
                    C(string.Format(CultureInfo.InvariantCulture, "{0:N2}", l.Quantity), TextAlignment.Right);
                    C(string.Format(CultureInfo.InvariantCulture, "{0:N2}", l.UnitPrice), TextAlignment.Right);
                    C(string.Format(CultureInfo.InvariantCulture, "{0:N2}", l.Quantity * l.UnitPrice), TextAlignment.Right);
                }

                _doc.Blocks.Add(new Paragraph(new Run(
                    $"Total: {total:N2} {head.Currency ?? "USD"}"))
                { FontWeight = FontWeights.SemiBold, Margin = new Thickness(0, 8, 0, 0) });

                if (!string.IsNullOrWhiteSpace(head.Description))
                {
                    _doc.Blocks.Add(new Paragraph(new Run($"Notes: {head.Description}"))
                    { Margin = new Thickness(0, 8, 0, 0) });
                }

                viewer.Document = _doc;
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, $"Failed to load invoice.\n\n{ex.Message}", "Invoice",
                    MessageBoxButton.OK, MessageBoxImage.Error);
                Close();
            }
        }

        private void Print_Click(object sender, RoutedEventArgs e)
        {
            if (_doc == null) return;

            var pd = new PrintDialog();
            if (pd.ShowDialog() == true)
            {
                _doc.PageHeight = pd.PrintableAreaHeight;
                _doc.PageWidth = pd.PrintableAreaWidth;
                _doc.PagePadding = new Thickness(50);
                _doc.ColumnGap = 0;
                _doc.ColumnWidth = pd.PrintableAreaWidth;

                pd.PrintDocument(((IDocumentPaginatorSource)_doc).DocumentPaginator, "Invoice");
            }
        }
    }
}
