using System;
using System.Globalization;
using System.Linq;
using System.Windows;
using System.Windows.Documents;
using System.Windows.Media;
using BestFlex.Application.Abstractions.Inventory;

namespace BestFlex.Shell.Printing
{
    public sealed class FlowDocGrnPrintEngine : IGrnPrintEngine
    {
        public FlowDocument CreateGrnDocument(ReceiveDraft draft, PurchaseReceiptResult result)
        {
            if (draft is null) throw new ArgumentNullException(nameof(draft));

            var doc = new FlowDocument
            {
                PagePadding = new Thickness(40),
                FontFamily = new FontFamily("Segoe UI"),
                FontSize = 12,
                Background = Brushes.White
            };

            var title = new Paragraph(new Run("Goods Received Note (GRN)"))
            {
                FontSize = 20,
                FontWeight = FontWeights.SemiBold,
                Foreground = Brushes.Black,
                Margin = new Thickness(0, 0, 0, 12)
            };
            doc.Blocks.Add(title);

            // Header grid
            var header = new Table { CellSpacing = 0 };
            header.Columns.Add(new TableColumn { Width = new GridLength(300) });
            header.Columns.Add(new TableColumn { Width = new GridLength(300) });
            var hGroup = new TableRowGroup();
            header.RowGroups.Add(hGroup);

            void AddHeaderRow(string leftLabel, string leftValue, string rightLabel, string rightValue)
            {
                var row = new TableRow();
                hGroup.Rows.Add(row);

                row.Cells.Add(MakeCell($"{leftLabel}: {leftValue}", isHeader: false));
                row.Cells.Add(MakeCell($"{rightLabel}: {rightValue}", isHeader: false));
            }

            AddHeaderRow("Supplier", draft.Supplier, "Date", draft.Date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture));
            AddHeaderRow("Document No", draft.DocumentNumber, "Receipt Id", result.ReceiptId.ToString());
            if (!string.IsNullOrWhiteSpace(draft.Notes))
                AddHeaderRow("Notes", draft.Notes!, "", "");

            doc.Blocks.Add(header);
            doc.Blocks.Add(new Paragraph(new Run("")) { Margin = new Thickness(0, 12, 0, 6) });

            // Lines table
            var table = new Table { CellSpacing = 0, BorderBrush = Brushes.Gray, BorderThickness = new Thickness(1) };
            table.Columns.Add(new TableColumn { Width = new GridLength(140) }); // Code
            table.Columns.Add(new TableColumn { Width = new GridLength(260) }); // Name
            table.Columns.Add(new TableColumn { Width = new GridLength(80) });  // Qty
            table.Columns.Add(new TableColumn { Width = new GridLength(100) }); // Unit Cost
            table.Columns.Add(new TableColumn { Width = new GridLength(120) }); // Line Total

            var body = new TableRowGroup();
            table.RowGroups.Add(body);

            // Header row
            var hdr = new TableRow();
            body.Rows.Add(hdr);
            hdr.Cells.Add(MakeCell("Code", true));
            hdr.Cells.Add(MakeCell("Name", true));
            hdr.Cells.Add(MakeCell("Qty", true));
            hdr.Cells.Add(MakeCell("Unit Cost", true));
            hdr.Cells.Add(MakeCell("Line Total", true));

            decimal grand = 0m;
            foreach (var ln in draft.Lines)
            {
                var lt = ln.Quantity * ln.UnitCost;
                grand += lt;

                var r = new TableRow();
                body.Rows.Add(r);
                r.Cells.Add(MakeCell(ln.Code));
                r.Cells.Add(MakeCell(ln.Name ?? ""));
                r.Cells.Add(MakeCell(ln.Quantity.ToString("0.###", CultureInfo.InvariantCulture)));
                r.Cells.Add(MakeCell(ln.UnitCost.ToString("0.###", CultureInfo.InvariantCulture)));
                r.Cells.Add(MakeCell(lt.ToString("0.###", CultureInfo.InvariantCulture)));
            }

            // Total row
            var totalRow = new TableRow();
            body.Rows.Add(totalRow);
            totalRow.Cells.Add(new TableCell(new Paragraph(new Run("Total"))) { ColumnSpan = 4, TextAlignment = TextAlignment.Right, Padding = new Thickness(6) });
            totalRow.Cells.Add(MakeCell(grand.ToString("0.###", CultureInfo.InvariantCulture), isHeader: true));

            doc.Blocks.Add(table);

            // Footer
            doc.Blocks.Add(new Paragraph(new Run("Received by: ______________________    Signature: ______________________"))
            {
                Margin = new Thickness(0, 20, 0, 0)
            });

            return doc;

            static TableCell MakeCell(string text, bool isHeader = false)
            {
                var p = new Paragraph(new Run(text)) { Margin = new Thickness(0), Padding = new Thickness(6) };
                var cell = new TableCell(p)
                {
                    BorderBrush = Brushes.LightGray,
                    BorderThickness = new Thickness(0.5),
                    TextAlignment = isHeader ? TextAlignment.Left : TextAlignment.Left
                };
                if (isHeader)
                {
                    p.FontWeight = FontWeights.SemiBold;
                    p.Foreground = Brushes.Black;
                    cell.Background = new SolidColorBrush(Color.FromRgb(245, 247, 250));
                }
                return cell;
            }
        }
    }
}
