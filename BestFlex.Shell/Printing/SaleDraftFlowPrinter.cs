using System.Linq;
using System.Windows;
using System.Windows.Documents;
using System.Windows.Media;
using BestFlex.Shell.Models;

namespace BestFlex.Shell.Printing
{
    public static class SaleDraftFlowPrinter
    {
        public static FlowDocument Build(SaleDraft draft)
        {
            var doc = new FlowDocument
            {
                PagePadding = new Thickness(40),
                ColumnWidth = double.PositiveInfinity,
                FontFamily = new FontFamily("Segoe UI"),
                FontSize = 12
            };

            // Header
            var h1 = new Paragraph(new Run("INVOICE"))
            {
                FontSize = 24,
                FontWeight = FontWeights.SemiBold,
                Margin = new Thickness(0, 0, 0, 12)
            };
            doc.Blocks.Add(h1);

            var meta = new Table { CellSpacing = 0 };
            meta.Columns.Add(new TableColumn { Width = new GridLength(300) });
            meta.Columns.Add(new TableColumn { Width = new GridLength(300) });
            var mr = new TableRowGroup();
            meta.RowGroups.Add(mr);

            AddMetaRow(mr, "Invoice No:", draft.InvoiceNumber, "Date:", draft.InvoiceDate.ToShortDateString());
            AddMetaRow(mr, "Customer:", draft.CustomerName ?? "-", "Currency:", draft.Currency);
            doc.Blocks.Add(meta);

            doc.Blocks.Add(new Paragraph(new Run(" ")) { Margin = new Thickness(0, 6, 0, 0) });

            // Lines table
            var table = new Table { CellSpacing = 0 };
            table.Columns.Add(new TableColumn { Width = new GridLength(110) }); // Code
            table.Columns.Add(new TableColumn { Width = new GridLength(1, GridUnitType.Star) }); // Name
            table.Columns.Add(new TableColumn { Width = new GridLength(70) });  // Qty
            table.Columns.Add(new TableColumn { Width = new GridLength(90) });  // Price
            table.Columns.Add(new TableColumn { Width = new GridLength(100) }); // Total

            var header = new TableRowGroup();
            var body = new TableRowGroup();
            table.RowGroups.Add(header);
            table.RowGroups.Add(body);

            header.Rows.Add(BuildHeaderRow("Code", "Name", "Qty", "Price", "Total"));

            foreach (var l in draft.Lines.Where(x => !string.IsNullOrWhiteSpace(x.Code)))
            {
                var r = new TableRow();
                AddCell(r, l.Code);
                AddCell(r, l.Name);
                AddCell(r, l.Qty.ToString("0.##"), TextAlignment.Right);
                AddCell(r, l.Price.ToString("0.##"), TextAlignment.Right);
                AddCell(r, l.Total.ToString("0.##"), TextAlignment.Right);
                body.Rows.Add(r);
            }
            doc.Blocks.Add(table);

            // Totals
            var tot = new Paragraph { Margin = new Thickness(0, 10, 0, 0), TextAlignment = TextAlignment.Right };
            tot.Inlines.Add(new Bold(new Run($"Subtotal: {draft.Subtotal:0.##}   ")));
            tot.Inlines.Add(new Run($"Discount %: {draft.DiscountPercent:0.##}   "));
            tot.Inlines.Add(new Run($"Tax %: {draft.TaxPercent:0.##}   "));
            tot.Inlines.Add(new Bold(new Run($"Grand Total: {draft.GrandTotal:0.##}")));
            doc.Blocks.Add(tot);

            // Footer note (use gray color rather than Opacity on Paragraph)
            var footer = new Paragraph(new Run("Thank you for your business."))
            {
                Margin = new Thickness(0, 20, 0, 0),
                FontSize = 11,
                Foreground = Brushes.Gray
            };
            doc.Blocks.Add(footer);

            return doc;
        }

        private static TableRow BuildHeaderRow(params string[] cells)
        {
            var r = new TableRow();
            foreach (var c in cells)
            {
                var p = new Paragraph(new Run(c)) { FontWeight = FontWeights.SemiBold, Margin = new Thickness(4, 4, 4, 4) };
                var cell = new TableCell(p) { BorderBrush = Brushes.LightGray, BorderThickness = new Thickness(0, 0, 0, 1) };
                r.Cells.Add(cell);
            }
            return r;
        }

        private static void AddCell(TableRow r, string? text, TextAlignment align = TextAlignment.Left)
        {
            var p = new Paragraph(new Run(text ?? "-")) { Margin = new Thickness(4, 4, 4, 4), TextAlignment = align };
            var cell = new TableCell(p) { BorderBrush = Brushes.Gainsboro, BorderThickness = new Thickness(0, 0, 0, 1) };
            r.Cells.Add(cell);
        }

        private static void AddMetaRow(TableRowGroup g, string l1, string? v1, string l2, string? v2)
        {
            var tr = new TableRow();
            tr.Cells.Add(new TableCell(new Paragraph(new Bold(new Run(l1)))) { BorderThickness = new Thickness(0) });
            tr.Cells.Add(new TableCell(new Paragraph(new Run(v1 ?? "-"))) { BorderThickness = new Thickness(0) });
            tr.Cells.Add(new TableCell(new Paragraph(new Bold(new Run(l2)))) { BorderThickness = new Thickness(0) });
            tr.Cells.Add(new TableCell(new Paragraph(new Run(v2 ?? "-"))) { BorderThickness = new Thickness(0) });
            g.Rows.Add(tr);
        }
    }
}
