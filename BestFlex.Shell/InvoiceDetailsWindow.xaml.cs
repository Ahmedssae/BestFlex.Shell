using BestFlex.Application.Abstractions;
using BestFlex.Shell.ViewModels;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Microsoft.Win32;
using System;
using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace BestFlex.Shell
{
    public partial class InvoiceDetailsWindow : Window
    {
        private readonly InvoiceDetailsViewModel _vm;
        private readonly IInvoicePdfExporter _pdfExporter;
        private readonly IOptionsMonitor<CompanySettings> _companyOptions;
        private readonly IOptionsMonitor<PrintTemplateSettings> _printOptions;
        public int InvoiceId { get; set; }

        public InvoiceDetailsWindow()
        {
            InitializeComponent();

            var app = (App)System.Windows.Application.Current;
            PreviewKeyDown += InvoiceDetailsWindow_PreviewKeyDown;


            _vm = app.Services.GetRequiredService<InvoiceDetailsViewModel>();
            _pdfExporter = app.Services.GetRequiredService<IInvoicePdfExporter>();
            _companyOptions = app.Services.GetRequiredService<IOptionsMonitor<CompanySettings>>();
            _printOptions = app.Services.GetRequiredService<IOptionsMonitor<PrintTemplateSettings>>();
            DataContext = _vm;

            Loaded += async (_, __) =>
            {
                if (InvoiceId <= 0)
                {
                    MessageBox.Show("Invalid invoice id.", "Invoice",
                        MessageBoxButton.OK, MessageBoxImage.Warning);
                    Close();
                    return;
                }
                await _vm.LoadAsync(InvoiceId);

            };
        }
        private void InvoiceDetailsWindow_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            if ((Keyboard.Modifiers == ModifierKeys.Control) && e.Key == Key.P)
            {
                Print_Click(this, new RoutedEventArgs());
                e.Handled = true;
                return;
            }
            if ((Keyboard.Modifiers == (ModifierKeys.Control | ModifierKeys.Shift)) && e.Key == Key.P)
            {
                Preview_Click(this, new RoutedEventArgs());
                e.Handled = true;
                return;
            }
            if ((Keyboard.Modifiers == ModifierKeys.Control) && e.Key == Key.E)
            {
                ExportPdf_Click(this, new RoutedEventArgs());
                e.Handled = true;
                return;
            }
            if (e.Key == Key.Escape)
            {
                Close_Click(this, new RoutedEventArgs());
                e.Handled = true;
            }
        }

        private static string SafeFileName(string name)
        {
            foreach (var c in Path.GetInvalidFileNameChars())
                name = name.Replace(c, '_');
            return name;
        }

        private void Preview_Click(object sender, RoutedEventArgs e)
        {
            var doc = BuildDocument(_vm);
            var wnd = new PrintPreviewWindow { Owner = this };
            wnd.Load(doc);
            wnd.ShowDialog();
        }
        private async void ExportPdf_Click(object sender, RoutedEventArgs e)
        {
            var defaultName = SafeFileName($"INV_{_vm.InvoiceNo}_{_vm.Customer}_{_vm.IssuedAt:yyyyMMdd}.pdf");
            var sfd = new SaveFileDialog
            {
                Filter = "PDF files (*.pdf)|*.pdf",
                FileName = defaultName,
                InitialDirectory = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments)
            };
            if (sfd.ShowDialog() != true) return;

            try
            {
                // Delegate subtotal/discount/tax/total calculation and DTO construction
                // to the ViewModel. This keeps the window code-behind UI-focused and
                // makes the business logic testable in the VM.
                var companySettings = _companyOptions.CurrentValue;
                var printSettings = _printOptions.CurrentValue;
                var dto = _vm.PrepareInvoicePrintData(companySettings, printSettings);

                var bytes = await _pdfExporter.RenderPdfAsync(dto);
                File.WriteAllBytes(sfd.FileName, bytes);

                var openNow = MessageBox.Show("PDF saved successfully.\nOpen it now?",
                                              "BestFlex", MessageBoxButton.YesNo, MessageBoxImage.Information);
                if (openNow == MessageBoxResult.Yes)
                {
                    try { Process.Start(new ProcessStartInfo { FileName = sfd.FileName, UseShellExecute = true }); }
                    catch { /* ignore */ }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show("Failed to create PDF:\n" + ex.Message, "BestFlex",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
        private void Close_Click(object sender, RoutedEventArgs e) => Close();

        private void Print_Click(object sender, RoutedEventArgs e)
        {
            var dlg = new PrintDialog();
            if (dlg.ShowDialog() != true) return;

            var doc = BuildDocument(_vm);
            doc.PageHeight = dlg.PrintableAreaHeight;
            doc.PageWidth = dlg.PrintableAreaWidth;

            IDocumentPaginatorSource dps = doc;
            dlg.PrintDocument(dps.DocumentPaginator, $"Invoice {_vm.InvoiceNo}");
        }

        private FlowDocument BuildDocument(InvoiceDetailsViewModel vm)
        {
            var c = _companyOptions.CurrentValue;
            var p = _printOptions.CurrentValue;

            var doc = new FlowDocument
            {
                PagePadding = new Thickness(p.Margin > 0 ? p.Margin : 48),
                FontFamily = new FontFamily("Segoe UI"),
                FontSize = 12
            };

            // Header (as before) ... use c.Name/c.Address/c.Phone/c.TaxNo/c.LogoPath
            // [KEEP your existing header code – unchanged except it uses 'c' not '_company']

            // --- snip: paste your existing header block here (unchanged) ---

            // Separator
            doc.Blocks.Add(new BlockUIContainer(new Border
            {
                BorderBrush = Brushes.Gray,
                BorderThickness = new Thickness(0, 0, 0, 1),
                Margin = new Thickness(0, 6, 0, 8)
            }));

            // Meta section (unchanged)
            // [KEEP your existing meta section code]

            // === Lines Table (conditional columns) ===
            var table = new Table { CellSpacing = 0, BorderBrush = Brushes.Gray, BorderThickness = new Thickness(0.5) };
            if (p.ShowCode) table.Columns.Add(new TableColumn { Width = new GridLength(120) });
            if (p.ShowName) table.Columns.Add(new TableColumn { Width = new GridLength(1, GridUnitType.Star) });
            if (p.ShowQty) table.Columns.Add(new TableColumn { Width = new GridLength(80) });
            if (p.ShowUnitPrice) table.Columns.Add(new TableColumn { Width = new GridLength(100) });
            if (p.ShowLineTotal) table.Columns.Add(new TableColumn { Width = new GridLength(100) });

            var group = new TableRowGroup();
            table.RowGroups.Add(group);

            var header = new TableRow { Background = Brushes.LightGray };
            if (p.ShowCode) header.Cells.Add(Cell("Code", true));
            if (p.ShowName) header.Cells.Add(Cell("Name", true));
            if (p.ShowQty) header.Cells.Add(Cell("Qty", true, TextAlignment.Right));
            if (p.ShowUnitPrice) header.Cells.Add(Cell("Price", true, TextAlignment.Right));
            if (p.ShowLineTotal) header.Cells.Add(Cell("Total", true, TextAlignment.Right));
            group.Rows.Add(header);

            foreach (var ln in vm.Lines)
            {
                var r = new TableRow();
                if (p.ShowCode) r.Cells.Add(Cell(ln.Code));
                if (p.ShowName) r.Cells.Add(Cell(ln.Name));
                if (p.ShowQty) r.Cells.Add(Cell($"{ln.Qty:N2}", false, TextAlignment.Right));
                if (p.ShowUnitPrice) r.Cells.Add(Cell($"{ln.UnitPrice:N2}", false, TextAlignment.Right));
                if (p.ShowLineTotal) r.Cells.Add(Cell($"{ln.LineTotal:N2}", false, TextAlignment.Right));
                group.Rows.Add(r);
            }
            doc.Blocks.Add(table);

            // Totals
            doc.Blocks.Add(new Paragraph(new Run($"Total: {vm.Total:N2} {vm.Currency}"))
            {
                FontSize = 16,
                FontWeight = FontWeights.SemiBold,
                TextAlignment = TextAlignment.Right,
                Margin = new Thickness(0, 12, 0, 0)
            });

            // Footer note (optional)
            if (!string.IsNullOrWhiteSpace(p.FooterNote))
                doc.Blocks.Add(new Paragraph(new Run(p.FooterNote)) { FontStyle = FontStyles.Italic, FontSize = 10, Margin = new Thickness(0, 8, 0, 0) });

            return doc;

            static TableCell Cell(string text, bool header = false, TextAlignment align = TextAlignment.Left) =>
                new TableCell(new Paragraph(new Run(text)) { TextAlignment = align })
                {
                    Padding = new Thickness(4),
                    FontWeight = header ? FontWeights.SemiBold : FontWeights.Normal,
                    BorderBrush = Brushes.Transparent
                };
        }

    }
}
