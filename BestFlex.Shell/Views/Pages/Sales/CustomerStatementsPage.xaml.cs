using BestFlex.Persistence.Data;
using BestFlex.Shell.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;

namespace BestFlex.Shell.Pages
{
    public partial class CustomerStatementsPage : UserControl
    {
        private readonly List<Row> _rows = new();

        public CustomerStatementsPage()
        {
            InitializeComponent();
            grid.ItemsSource = _rows;
        }

        private async void UserControl_Loaded(object sender, RoutedEventArgs e)
        {
            dpFrom.SelectedDate = new DateTime(DateTime.Today.Year, DateTime.Today.Month, 1);
            dpTo.SelectedDate = DateTime.Today;

            await LoadCustomersAsync();
        }

        private async Task LoadCustomersAsync()
        {
            try
            {
                ShowOverlay(true);

                var sp = ((App)System.Windows.Application.Current).Services;
                using var scope = sp.CreateScope();
                var db = scope.ServiceProvider.GetRequiredService<BestFlexDbContext>();

                var customers = await db.CustomerAccounts
                    .AsNoTracking()
                    .OrderBy(c => c.Name)
                    .Select(c => new { c.Id, c.Name })
                    .ToListAsync();

                cmbCustomer.ItemsSource = customers;
            }
            catch (Exception ex)
            {
                MessageBox.Show(Window.GetWindow(this)!,
                    $"Failed to load customers.\n\n{ex.Message}",
                    "Customer Statements",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                ShowOverlay(false);
            }
        }

        // ----- UI actions -----
        private async void BtnLoad_Click(object sender, RoutedEventArgs e)
        {
            await LoadAsync();
        }

        private async void BtnClear_Click(object sender, RoutedEventArgs e)
        {
            cmbCustomer.SelectedIndex = -1;
            dpFrom.SelectedDate = null;
            dpTo.SelectedDate = null;
            _rows.Clear();
            grid.Items.Refresh();
            txtTotalDebit.Text = txtTotalCredit.Text = txtClosing.Text = "";
            await Task.CompletedTask;
        }

        private void BtnExport_Click(object sender, RoutedEventArgs e)
        {
            var cols = new (string, Func<Row, object?>)[]
            {
                ("Date",    r => r.Date.ToString("yyyy-MM-dd")),
                ("DocNo",   r => r.DocNo),
                ("Type",    r => r.Type),
                ("Debit",   r => r.Debit),
                ("Credit",  r => r.Credit),
                ("Balance", r => r.Balance),
                ("Notes",   r => r.Notes),
            };
            CsvExporter.Export(_rows, cols, "customer-statement.csv");
        }

        private void BtnPrint_Click(object sender, RoutedEventArgs e)
        {
            var doc = BuildDoc();

            var pd = new PrintDialog();
            if (pd.ShowDialog() == true)
            {
                doc.PageHeight = pd.PrintableAreaHeight;
                doc.PageWidth = pd.PrintableAreaWidth;
                doc.PagePadding = new Thickness(50);
                doc.ColumnGap = 0;
                doc.ColumnWidth = pd.PrintableAreaWidth;

                pd.PrintDocument(((IDocumentPaginatorSource)doc).DocumentPaginator, "Customer Statement");
            }
        }

        private void BtnAddCustomer_Click(object sender, RoutedEventArgs e)
        {
            var wnd = new BestFlex.Shell.Windows.QuickAddCustomerWindow
            {
                Owner = Window.GetWindow(this)
            };
            if (wnd.ShowDialog() == true)
            {
                _ = LoadCustomersAsync().ContinueWith(_ =>
                {
                    Dispatcher.Invoke(() =>
                    {
                        var created = wnd.CreatedCustomer;
                        var idProp = created?.GetType().GetProperty("Id");
                        if (idProp != null)
                            cmbCustomer.SelectedValue = Convert.ToInt32(idProp.GetValue(created), CultureInfo.InvariantCulture);
                    });
                });
            }
        }

        // ----- Core loading (SQLite-safe) -----
        private async Task LoadAsync()
        {
            try
            {
                if (cmbCustomer.SelectedValue == null)
                {
                    MessageBox.Show(Window.GetWindow(this)!, "Please select a customer.", "Customer Statements",
                        MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                ShowOverlay(true);

                var customerId = (int)cmbCustomer.SelectedValue;
                var from = dpFrom.SelectedDate?.Date;
                var to = dpTo.SelectedDate?.Date;

                var sp = ((App)System.Windows.Application.Current).Services;
                using var scope = sp.CreateScope();
                var db = scope.ServiceProvider.GetRequiredService<BestFlexDbContext>();

                // 1) Fetch invoice headers for the customer and date filter
                var invHeadersQuery = db.SellingInvoices.AsNoTracking()
                    .Where(i => i.CustomerAccountId == customerId);

                if (from.HasValue) invHeadersQuery = invHeadersQuery.Where(i => i.IssuedAt >= from.Value);
                if (to.HasValue) invHeadersQuery = invHeadersQuery.Where(i => i.IssuedAt <= to.Value.AddDays(1).AddTicks(-1));

                var invHeaders = await invHeadersQuery
                    .Select(i => new { i.Id, i.InvoiceNo, i.IssuedAt, i.Description })
                    .OrderBy(i => i.IssuedAt)
                    .ToListAsync();

                // 2) Bring invoice items for the selected invoice ids, then sum lines IN MEMORY (SQLite-safe)
                var ids = invHeaders.Select(h => h.Id).ToList();
                var lineItems = await db.SellingInvoiceItems
                    .AsNoTracking()
                    .Where(it => ids.Contains(it.SellingInvoiceId))
                    .Select(it => new { it.SellingInvoiceId, it.Quantity, it.UnitPrice })
                    .ToListAsync();

                var amountByInvoiceId = lineItems
                    .GroupBy(x => x.SellingInvoiceId)
                    .ToDictionary(
                        g => g.Key,
                        g => g.Aggregate(0m, (sum, x) => sum + (x.Quantity * x.UnitPrice)));

                // 3) Build rows + running balance
                _rows.Clear();
                decimal balance = 0m;
                decimal totalDebit = 0m, totalCredit = 0m;

                foreach (var h in invHeaders)
                {
                    var amount = amountByInvoiceId.TryGetValue(h.Id, out var v) ? v : 0m;

                    var r = new Row
                    {
                        Date = h.IssuedAt.Date,
                        DocNo = h.InvoiceNo,
                        Type = "Invoice",
                        Debit = amount,            // invoices raise debit
                        Credit = 0m,
                        Notes = h.Description
                    };
                    balance += r.Debit - r.Credit;
                    r.Balance = balance;

                    totalDebit += r.Debit;
                    totalCredit += r.Credit;
                    _rows.Add(r);
                }

                grid.Items.Refresh();

                txtTotalDebit.Text = totalDebit.ToString("N2", CultureInfo.InvariantCulture);
                txtTotalCredit.Text = totalCredit.ToString("N2", CultureInfo.InvariantCulture);
                txtClosing.Text = balance.ToString("N2", CultureInfo.InvariantCulture);
            }
            catch (Exception ex)
            {
                MessageBox.Show(Window.GetWindow(this)!,
                    $"Failed to load invoices.\n\n{ex.Message}",
                    "Customer Statements",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                ShowOverlay(false);
            }
        }

        private FlowDocument BuildDoc()
        {
            var doc = new FlowDocument
            {
                FontFamily = new FontFamily("Segoe UI"),
                FontSize = 12,
                PagePadding = new Thickness(40)
            };

            var title = new Paragraph(new Run("Customer Statement"))
            {
                FontSize = 18,
                FontWeight = FontWeights.SemiBold,
                Margin = new Thickness(0, 0, 0, 6)
            };
            doc.Blocks.Add(title);

            var custName = (cmbCustomer.SelectedItem as dynamic)?.Name ?? "(customer)";
            var range = $"{(dpFrom.SelectedDate.HasValue ? dpFrom.SelectedDate.Value.ToString("yyyy-MM-dd") : "…")} → {(dpTo.SelectedDate.HasValue ? dpTo.SelectedDate.Value.ToString("yyyy-MM-dd") : "…")}";
            doc.Blocks.Add(new Paragraph(new Run($"Customer: {custName}   •   Range: {range}"))
            { Foreground = Brushes.Gray, Margin = new Thickness(0, 0, 0, 12) });

            var table = new Table();
            doc.Blocks.Add(table);

            var widths = new GridLength[] {
                new GridLength(140), // Date
                new GridLength(160), // DocNo
                new GridLength(160), // Type
                new GridLength(110), // Debit
                new GridLength(110), // Credit
                new GridLength(120), // Balance
                new GridLength(1, GridUnitType.Star) // Notes
            };
            foreach (var w in widths) table.Columns.Add(new TableColumn { Width = w });

            var header = new TableRowGroup();
            var hr = new TableRow();
            header.Rows.Add(hr);
            table.RowGroups.Add(header);

            void H(string t) => hr.Cells.Add(new TableCell(new Paragraph(new Run(t)))
            {
                FontWeight = FontWeights.SemiBold,
                Padding = new Thickness(4, 2, 4, 2),
                BorderBrush = Brushes.LightGray,
                BorderThickness = new Thickness(0, 0, 0, 1)
            });
            H("Date"); H("Doc No"); H("Type"); H("Debit"); H("Credit"); H("Balance"); H("Notes");

            var body = new TableRowGroup();
            table.RowGroups.Add(body);

            foreach (var r in _rows)
            {
                var tr = new TableRow();
                body.Rows.Add(tr);

                void C(object? t, TextAlignment align = TextAlignment.Left)
                    => tr.Cells.Add(new TableCell(new Paragraph(new Run(t?.ToString() ?? "")) { TextAlignment = align })
                    { Padding = new Thickness(4, 2, 4, 2) });

                C(r.Date.ToString("yyyy-MM-dd"));
                C(r.DocNo);
                C(r.Type);
                C(r.Debit.ToString("N2"), TextAlignment.Right);
                C(r.Credit.ToString("N2"), TextAlignment.Right);
                C(r.Balance.ToString("N2"), TextAlignment.Right);
                C(r.Notes);
            }

            var totDebit = _rows.Sum(x => x.Debit);
            var totCredit = _rows.Sum(x => x.Credit);
            var closing = _rows.LastOrDefault()?.Balance ?? 0m;

            doc.Blocks.Add(new Paragraph(new Run(
                $"Total Debit: {totDebit:N2}   •   Total Credit: {totCredit:N2}   •   Closing: {closing:N2}"))
            { Margin = new Thickness(0, 8, 0, 0), FontWeight = FontWeights.SemiBold });

            return doc;
        }

        private void ShowOverlay(bool on)
        {
            overlay.Visibility = on ? Visibility.Visible : Visibility.Collapsed;
            overlay.IsHitTestVisible = on;
        }

        private sealed class Row
        {
            public DateTime Date { get; set; }
            public string DocNo { get; set; } = "";
            public string Type { get; set; } = "";
            public decimal Debit { get; set; }
            public decimal Credit { get; set; }
            public decimal Balance { get; set; }
            public string Notes { get; set; } = "";
        }
    }
}
