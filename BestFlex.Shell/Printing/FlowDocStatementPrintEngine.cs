using System;
using System.Globalization;
using System.Linq;
using System.Windows;
using System.Windows.Documents;
using System.Windows.Media;
using BestFlex.Application.Abstractions.Statements;

namespace BestFlex.Shell.Printing
{
    public sealed class FlowDocStatementPrintEngine : IStatementPrintEngine
    {
        public FlowDocument Create(StatementResult result)
        {
            var doc = new FlowDocument
            {
                PagePadding = new Thickness(40),
                FontFamily = new FontFamily("Segoe UI"),
                FontSize = 12,
                Background = Brushes.White
            };

            doc.Blocks.Add(new Paragraph(new Run("Customer Statement"))
            {
                FontSize = 20,
                FontWeight = FontWeights.SemiBold,
                Foreground = Brushes.Black,
                Margin = new Thickness(0, 0, 0, 12)
            });

            var header = new Table { CellSpacing = 0 };
            header.Columns.Add(new TableColumn { Width = new GridLength(350) });
            header.Columns.Add(new TableColumn { Width = new GridLength(250) });
            var g = new TableRowGroup();
            header.RowGroups.Add(g);

            void Row(string l, string lv, string r, string rv)
            {
                var tr = new TableRow();
                tr.Cells.Add(Cell($"{l}: {lv}"));
                tr.Cells.Add(Cell($"{r}: {rv}"));
                g.Rows.Add(tr);
            }

            Row("Customer", result.Customer, "Period",
                $"{result.From:yyyy-MM-dd} → {result.To:yyyy-MM-dd}");
            Row("Opening Balance", result.OpeningBalance.ToString("0.###", CultureInfo.InvariantCulture),
                "Closing Balance", result.ClosingBalance.ToString("0.###", CultureInfo.InvariantCulture));

            doc.Blocks.Add(header);
            doc.Blocks.Add(new Paragraph(new Run("")) { Margin = new Thickness(0, 8, 0, 6) });

            // Lines
            var t = new Table { CellSpacing = 0, BorderBrush = Brushes.Gray, BorderThickness = new Thickness(1) };
            t.Columns.Add(new TableColumn { Width = new GridLength(110) }); // Date
            t.Columns.Add(new TableColumn { Width = new GridLength(140) }); // DocNo
            t.Columns.Add(new TableColumn { Width = new GridLength(120) }); // DocType
            t.Columns.Add(new TableColumn { Width = new GridLength(110) }); // Debit
            t.Columns.Add(new TableColumn { Width = new GridLength(110) }); // Credit
            t.Columns.Add(new TableColumn { Width = new GridLength(120) }); // Balance
            t.Columns.Add(new TableColumn { Width = new GridLength(1, GridUnitType.Star) }); // Notes

            var body = new TableRowGroup();
            t.RowGroups.Add(body);

            // Header row
            var h = new TableRow();
            body.Rows.Add(h);
            foreach (var s in new[] { "Date", "Doc No", "Type", "Debit", "Credit", "Balance", "Notes" })
                h.Cells.Add(HeaderCell(s));

            foreach (var ln in result.Lines)
            {
                var r = new TableRow();
                body.Rows.Add(r);
                r.Cells.Add(Cell(ln.Date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)));
                r.Cells.Add(Cell(ln.DocNo));
                r.Cells.Add(Cell(ln.DocType));
                r.Cells.Add(Cell(ln.Debit == 0 ? "" : ln.Debit.ToString("0.###", CultureInfo.InvariantCulture)));
                r.Cells.Add(Cell(ln.Credit == 0 ? "" : ln.Credit.ToString("0.###", CultureInfo.InvariantCulture)));
                r.Cells.Add(Cell(ln.Balance.ToString("0.###", CultureInfo.InvariantCulture)));
                r.Cells.Add(Cell(ln.Notes ?? ""));
            }

            doc.Blocks.Add(t);

            if (result.Aging is not null)
            {
                doc.Blocks.Add(new Paragraph(new Run("")) { Margin = new Thickness(0, 10, 0, 0) });
                var ag = new Table { CellSpacing = 0 };
                ag.Columns.Add(new TableColumn { Width = new GridLength(120) });
                ag.Columns.Add(new TableColumn { Width = new GridLength(120) });
                ag.Columns.Add(new TableColumn { Width = new GridLength(120) });
                ag.Columns.Add(new TableColumn { Width = new GridLength(120) });
                var rg = new TableRowGroup();
                ag.RowGroups.Add(rg);

                var hdr = new TableRow();
                hdr.Cells.Add(HeaderCell("0–30"));
                hdr.Cells.Add(HeaderCell("31–60"));
                hdr.Cells.Add(HeaderCell("61–90"));
                hdr.Cells.Add(HeaderCell(">90"));
                rg.Rows.Add(hdr);

                var v = new TableRow();
                v.Cells.Add(Cell(result.Aging.A0To30.ToString("0.###", CultureInfo.InvariantCulture)));
                v.Cells.Add(Cell(result.Aging.A31To60.ToString("0.###", CultureInfo.InvariantCulture)));
                v.Cells.Add(Cell(result.Aging.A61To90.ToString("0.###", CultureInfo.InvariantCulture)));
                v.Cells.Add(Cell(result.Aging.AOver90.ToString("0.###", CultureInfo.InvariantCulture)));
                rg.Rows.Add(v);

                doc.Blocks.Add(ag);
            }

            return doc;

            static TableCell Cell(string text)
            {
                var p = new Paragraph(new Run(text)) { Margin = new Thickness(0), Padding = new Thickness(6) };
                return new TableCell(p)
                {
                    BorderBrush = Brushes.LightGray,
                    BorderThickness = new Thickness(0.5)
                };
            }

            static TableCell HeaderCell(string text)
            {
                var p = new Paragraph(new Run(text))
                {
                    Margin = new Thickness(0),
                    Padding = new Thickness(6),
                    FontWeight = FontWeights.SemiBold,
                    Foreground = Brushes.Black
                };
                return new TableCell(p)
                {
                    BorderBrush = Brushes.LightGray,
                    BorderThickness = new Thickness(0.5),
                    Background = new SolidColorBrush(Color.FromRgb(245, 247, 250))
                };
            }
        }
    }
}
