using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using BestFlex.Application.Abstractions;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using System.IO;

namespace BestFlex.Printing
{
    public class InvoicePdfExporter : IInvoicePdfExporter
    {
        public Task<byte[]> RenderPdfAsync(InvoicePrintData data, CancellationToken ct = default)
        {
            var bytes = Document.Create(container =>
            {
                container.Page(page =>
                {
                    page.Size(data.PageSize?.ToUpperInvariant() == "A5" ? PageSizes.A5 : PageSizes.A4);
                    page.Margin(data.Margin > 0 ? data.Margin : 20);
                    page.DefaultTextStyle(x => x.FontSize(10));
                    page.PageColor(Colors.White);

                    // ===== HEADER =====
                    page.Header().Row(row =>
                    {
                        var logoBytes = TryLoadLogoBytes(data.CompanyLogoPath);
                        if (logoBytes != null)
                            row.AutoItem().Height(48).Image(logoBytes).FitHeight();

                        row.RelativeItem().Column(col =>
                        {
                            col.Item().Text(data.CompanyName).SemiBold().FontSize(18);

                            var line = string.Join(" • ", new[]
                            {
                                data.CompanyAddress,
                                data.CompanyPhone,
                                string.IsNullOrWhiteSpace(data.CompanyTaxNo) ? null : ("Tax: " + data.CompanyTaxNo)
                            }.Where(x => !string.IsNullOrWhiteSpace(x)));

                            if (!string.IsNullOrWhiteSpace(line))
                                col.Item().Text(line);
                        });

                        row.AutoItem().Column(col =>
                        {
                            col.Item().AlignRight().Text($"Invoice: {data.InvoiceNo}").SemiBold();
                            col.Item().AlignRight().Text(data.IssuedAt.ToString("yyyy-MM-dd HH:mm"));
                        });
                    });

                    // ===== CONTENT =====
                    page.Content().Column(col =>
                    {
                        col.Item().PaddingBottom(6).Row(r =>
                        {
                            r.RelativeItem().Text(t =>
                            {
                                t.Span("Customer: ").SemiBold();
                                t.Span(data.CustomerName);
                                if (!string.IsNullOrWhiteSpace(data.CustomerAddress))
                                    t.Line(data.CustomerAddress);
                            });

                            r.AutoItem().Text(t =>
                            {
                                t.Span("Issuer: ").SemiBold();
                                t.Span(string.IsNullOrWhiteSpace(data.Issuer) ? "" : data.Issuer);
                            });
                        });

                        if (!string.IsNullOrWhiteSpace(data.Description))
                            col.Item().Text(data.Description);

                        // Items table
                        col.Item().Table(table =>
                        {
                            table.ColumnsDefinition(cols =>
                            {
                                if (data.ShowCode) cols.ConstantColumn(80);
                                if (data.ShowName) cols.RelativeColumn(3);
                                if (data.ShowQty) cols.ConstantColumn(60);
                                if (data.ShowUnitPrice) cols.ConstantColumn(80);
                                if (data.ShowLineTotal) cols.ConstantColumn(80);
                            });

                            table.Header(h =>
                            {
                                if (data.ShowCode) h.Cell().Element(Th).Text("Code");
                                if (data.ShowName) h.Cell().Element(Th).Text("Name");
                                if (data.ShowQty) h.Cell().Element(Th).AlignRight().Text("Qty");
                                if (data.ShowUnitPrice) h.Cell().Element(Th).AlignRight().Text("Price");
                                if (data.ShowLineTotal) h.Cell().Element(Th).AlignRight().Text("Total");

                                IContainer Th(IContainer c) =>
                                    c.BorderBottom(1).PaddingVertical(6).DefaultTextStyle(x => x.SemiBold());
                            });

                            foreach (var ln in data.Lines)
                            {
                                if (data.ShowCode) table.Cell().Element(Td).Text(ln.Code);
                                if (data.ShowName) table.Cell().Element(Td).Text(ln.Name);
                                if (data.ShowQty) table.Cell().Element(Td).AlignRight().Text(ln.Qty.ToString("N2"));
                                if (data.ShowUnitPrice) table.Cell().Element(Td).AlignRight().Text(ln.UnitPrice.ToString("N2") + " " + data.Currency);
                                if (data.ShowLineTotal) table.Cell().Element(Td).AlignRight().Text(ln.LineTotal.ToString("N2") + " " + data.Currency);

                                IContainer Td(IContainer c) => c.PaddingVertical(4);
                            }
                        });

                        // Totals (with optional discount / tax)
                        col.Item().PaddingTop(6).Row(r =>
                        {
                            r.RelativeItem();

                            r.ConstantItem(250).Border(1).Padding(8).Column(tot =>
                            {
                                tot.Item().Row(rr =>
                                {
                                    rr.RelativeItem().Text("Subtotal:");
                                    rr.AutoItem().Text(data.Subtotal.ToString("N2") + " " + data.Currency);
                                });

                                if (data.DiscountAmount > 0)
                                {
                                    tot.Item().Row(rr =>
                                    {
                                        rr.RelativeItem().Text($"Discount ({data.DiscountPercent:N2}%):");
                                        rr.AutoItem().Text("-" + data.DiscountAmount.ToString("N2") + " " + data.Currency);
                                    });
                                }

                                if (data.TaxAmount > 0)
                                {
                                    tot.Item().Row(rr =>
                                    {
                                        rr.RelativeItem().Text($"Tax ({data.TaxPercent:N2}%):");
                                        rr.AutoItem().Text("+" + data.TaxAmount.ToString("N2") + " " + data.Currency);
                                    });
                                }

                                tot.Item().Row(rr =>
                                {
                                    rr.RelativeItem().Text("Total:").SemiBold();
                                    rr.AutoItem().Text(data.Total.ToString("N2") + " " + data.Currency).SemiBold();
                                });
                            });
                        });

                        if (!string.IsNullOrWhiteSpace(data.FooterNote))
                            col.Item().PaddingTop(8).Text(data.FooterNote!).Italic().FontSize(9);
                    });

                    // ===== FOOTER =====
                    page.Footer().AlignRight().Text(x =>
                    {
                        x.Span("Powered by ").Light();
                        x.Span("BestFlex").SemiBold();
                    });
                });
            }).GeneratePdf();

            return Task.FromResult(bytes);
        }

        // Only PNG/JPG are attempted; any failure returns null (no crash)
        private static byte[]? TryLoadLogoBytes(string? path)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(path)) return null;

                var full = Path.GetFullPath(path);
                if (!File.Exists(full)) return null;

                var ext = Path.GetExtension(full).ToLowerInvariant();
                if (ext is not (".png" or ".jpg" or ".jpeg")) return null;

                return File.ReadAllBytes(full);
            }
            catch
            {
                return null;
            }
        }
    }
}
