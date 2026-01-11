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
        private readonly CustomerStatementsViewModel _vm;

        public CustomerStatementsPage()
        {
            InitializeComponent();
            _vm = new CustomerStatementsViewModel(((App)System.Windows.Application.Current).Services);
            grid.ItemsSource = _vm.Rows;
        }

        private async void UserControl_Loaded(object sender, RoutedEventArgs e)
        {
            dpFrom.SelectedDate = new DateTime(DateTime.Today.Year, DateTime.Today.Month, 1);
            dpTo.SelectedDate = DateTime.Today;

            await _vm.LoadCustomersAsync();
            cmbCustomer.ItemsSource = _vm.Customers;
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
            _vm.Rows.Clear();
            grid.Items.Refresh();
            txtTotalDebit.Text = txtTotalCredit.Text = txtClosing.Text = "";
            await Task.CompletedTask;
        }

        private void BtnExport_Click(object sender, RoutedEventArgs e)
        {
            var cols = new (string, Func<CustomerStatementsViewModel.Row, object?>)[]
            {
                ("Date",    r => r.Date.ToString("yyyy-MM-dd")),
                ("DocNo",   r => r.DocNo),
                ("Type",    r => r.Type),
                ("Debit",   r => r.Debit),
                ("Credit",  r => r.Credit),
                ("Balance", r => r.Balance),
                ("Notes",   r => r.Notes),
            };
            CsvExporter.Export(_vm.Rows, cols, "customer-statement.csv");
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
                _ = _vm.LoadCustomersAsync().ContinueWith(_ =>
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
            if (cmbCustomer.SelectedValue == null)
            {
                MessageBox.Show(Window.GetWindow(this)!, "Please select a customer.", "Customer Statements",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            ShowOverlay(true);
            try
            {
                var customerId = (int)cmbCustomer.SelectedValue;
                var from = dpFrom.SelectedDate?.Date;
                var to = dpTo.SelectedDate?.Date;

                await _vm.LoadAsync(customerId, from, to);

                grid.Items.Refresh();

                txtTotalDebit.Text = _vm.TotalDebit.ToString("N2", CultureInfo.InvariantCulture);
                txtTotalCredit.Text = _vm.TotalCredit.ToString("N2", CultureInfo.InvariantCulture);
                txtClosing.Text = _vm.Closing.ToString("N2", CultureInfo.InvariantCulture);
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

            foreach (var r in _vm.Rows)
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

            var totDebit = _vm.Rows.Sum(x => x.Debit);
            var totCredit = _vm.Rows.Sum(x => x.Credit);
            var closing = _vm.Rows.LastOrDefault()?.Balance ?? 0m;

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

        // Row type is provided by CustomerStatementsViewModel.Row
    }
}
