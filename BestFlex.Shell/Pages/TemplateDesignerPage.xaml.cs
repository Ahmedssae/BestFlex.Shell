using BestFlex.Persistence.Data;
using BestFlex.Shell.Printing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;

namespace BestFlex.Shell.Pages
{
    public partial class TemplateDesignerPage : UserControl
    {
        private readonly IServiceProvider _sp;
        private int _companyId = 1;

        private sealed class VersionItem
        {
            public int Id { get; set; }
            public string Label { get; set; } = "";
            public string Engine { get; set; } = "FlowDocument";
            public string Payload { get; set; } = "";
            public bool IsDefault { get; set; }
        }

        private readonly ObservableCollection<VersionItem> _history = new();

        public TemplateDesignerPage(IServiceProvider sp)
        {
            InitializeComponent();
            _sp = sp;

            this.Loaded += async (_, __) =>
            {
                using var scope = _sp.CreateScope();
                var db = scope.ServiceProvider.GetRequiredService<BestFlexDbContext>();
                var comp = await db.Companies.OrderBy(c => c.Id).FirstOrDefaultAsync();
                if (comp != null) _companyId = comp.Id;

                // Load current template
                var dbProvider = new DbInvoiceTemplateProvider(db);
                var tpl = dbProvider.GetTemplateForCompany(_companyId);
                XamlEditor.Text = tpl.Payload ?? string.Empty;
                ChkDefault.IsChecked = tpl.IsDefault;

                // Load versions
                await ReloadHistoryAsync();

                Preview_Click(null!, null!);
            };
        }

        private async System.Threading.Tasks.Task ReloadHistoryAsync()
        {
            using var scope = _sp.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<BestFlexDbContext>();

            _history.Clear();
            var items = await db.PrintTemplateVersions
                .AsNoTracking()
                .Where(v => v.CompanyId == _companyId && v.DocType == "invoice")
                .OrderByDescending(v => v.CreatedAtUtc)
                .Take(50)
                .Select(v => new VersionItem
                {
                    Id = v.Id,
                    Label = $"{v.CreatedAtUtc:yyyy-MM-dd HH:mm} {(v.IsDefault ? "[default]" : "[custom]")}",
                    Engine = v.Engine,
                    Payload = v.Payload,
                    IsDefault = v.IsDefault
                })
                .ToListAsync();

            foreach (var it in items) _history.Add(it);
            HistoryCombo.ItemsSource = _history;
        }

        private void Reset_Click(object sender, RoutedEventArgs e)
        {
            var def = new InMemoryInvoiceTemplateProvider().GetTemplateForCompany(_companyId);
            XamlEditor.Text = def.Payload ?? string.Empty;
            ChkDefault.IsChecked = true;
            Preview_Click(sender, e);
        }

        private void Save_Click(object sender, RoutedEventArgs e)
        {
            using var scope = _sp.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<BestFlexDbContext>();

            var editor = new DbInvoiceTemplateProvider(db);
            editor.SetTemplateForCompany(_companyId, new PrintTemplate
            {
                Name = "Custom",
                Engine = "FlowDocument",
                Payload = XamlEditor.Text,
                IsDefault = ChkDefault.IsChecked == true
            });

            MessageBox.Show("Template saved to database.", "Templates",
                MessageBoxButton.OK, MessageBoxImage.Information);

            _ = ReloadHistoryAsync();
        }

        private void LoadVersion_Click(object sender, RoutedEventArgs e)
        {
            if (HistoryCombo.SelectedItem is VersionItem sel)
            {
                XamlEditor.Text = sel.Payload ?? string.Empty;
                ChkDefault.IsChecked = sel.IsDefault;
                Preview_Click(sender, e);
            }
            else
            {
                MessageBox.Show("Select a history entry first.", "Templates", MessageBoxButton.OK, MessageBoxImage.Information);
            }
        }

        private void RestoreVersion_Click(object sender, RoutedEventArgs e)
        {
            if (HistoryCombo.SelectedItem is VersionItem sel)
            {
                // Save the selected version as current (respect its IsDefault flag)
                using var scope = _sp.CreateScope();
                var db = scope.ServiceProvider.GetRequiredService<BestFlexDbContext>();
                var editor = new DbInvoiceTemplateProvider(db);
                editor.SetTemplateForCompany(_companyId, new PrintTemplate
                {
                    Name = "Restored",
                    Engine = sel.Engine,
                    Payload = sel.Payload,
                    IsDefault = sel.IsDefault
                });

                MessageBox.Show("Version restored.", "Templates", MessageBoxButton.OK, MessageBoxImage.Information);
                _ = ReloadHistoryAsync();
            }
            else
            {
                MessageBox.Show("Select a history entry first.", "Templates", MessageBoxButton.OK, MessageBoxImage.Information);
            }
        }

        private void Preview_Click(object? sender, RoutedEventArgs? e)
        {
            try
            {
                var engine = _sp.GetRequiredService<IInvoicePrintEngine>();

                var runtimeTpl = new PrintTemplate
                {
                    Name = "Preview",
                    Engine = "FlowDocument",
                    Payload = XamlEditor.Text,
                    IsDefault = false
                };

                var draft = SampleDraft();
                var ctx = SampleCompanyContext();

                FlowDocument doc = engine.Render(draft, runtimeTpl, ctx);
                PreviewViewer.Document = (IDocumentPaginatorSource)doc;
            }
            catch (Exception ex)
            {
                MessageBox.Show("Preview error:\n" + ex.Message, "Templates",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private static BestFlex.Shell.Models.SaleDraft SampleDraft()
        {
            var d = new BestFlex.Shell.Models.SaleDraft
            {
                InvoiceNumber = "INV-PRV-001",
                InvoiceDate = DateTime.Today,
                CustomerName = "Walk-in",
                Currency = "USD",
                DiscountPercent = 5,
                TaxPercent = 10
            };
            d.Lines.Add(new BestFlex.Shell.Models.SaleDraftLine { Code = "P-0001", Name = "Sample Product", Qty = 2, Price = 10m });
            d.Lines.Add(new BestFlex.Shell.Models.SaleDraftLine { Code = "P-0002", Name = "USB Cable", Qty = 1, Price = 5m });
            d.Subtotal = d.Lines.Sum(x => x.Total);
            var afterDisc = d.Subtotal * 0.95m;
            d.GrandTotal = Math.Round(afterDisc * 1.10m, 2);
            return d;
        }

        private BestFlex.Shell.Printing.CompanyPrintContext SampleCompanyContext()
        {
            string? name = "My Company";
            try
            {
                using var scope = _sp.CreateScope();
                var db = scope.ServiceProvider.GetRequiredService<BestFlexDbContext>();
                var comp = db.Companies.AsNoTracking().OrderBy(c => c.Id).FirstOrDefault();
                if (comp != null) name = comp.Name;
            }
            catch { }

            return new BestFlex.Shell.Printing.CompanyPrintContext
            {
                CompanyId = _companyId,
                CompanyName = name,
                FooterNote = "Thank you for your business."
            };
        }
    }
}
