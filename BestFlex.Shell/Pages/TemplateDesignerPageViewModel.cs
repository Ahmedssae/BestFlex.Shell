using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Documents;
using BestFlex.Persistence.Data;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.EntityFrameworkCore;
using BestFlex.Shell.Printing;

namespace BestFlex.Shell.Pages
{
    public sealed class TemplateDesignerPageViewModel
    {
        private readonly IServiceProvider _sp;
        private int _companyId = 1;

        public string Payload { get; set; } = string.Empty;
        public bool IsDefault { get; set; }

        public ObservableCollection<VersionItem> History { get; } = new();

        public TemplateDesignerPageViewModel(IServiceProvider sp)
        {
            _sp = sp ?? throw new ArgumentNullException(nameof(sp));
        }

        public async Task InitializeAsync()
        {
            using var scope = _sp.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<BestFlexDbContext>();
            var comp = await db.Companies.OrderBy(c => c.Id).FirstOrDefaultAsync();
            if (comp != null) _companyId = comp.Id;

            // Load current template
            var dbProvider = new DbInvoiceTemplateProvider(db);
            var tpl = dbProvider.GetTemplateForCompany(_companyId);
            Payload = tpl.Payload ?? string.Empty;
            IsDefault = tpl.IsDefault;

            await ReloadHistoryAsync();
        }

        public async Task ReloadHistoryAsync()
        {
            using var scope = _sp.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<BestFlexDbContext>();

            History.Clear();
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

            foreach (var it in items) History.Add(it);
        }

        public void ResetToDefault()
        {
            var def = new InMemoryInvoiceTemplateProvider().GetTemplateForCompany(_companyId);
            Payload = def.Payload ?? string.Empty;
            IsDefault = def.IsDefault;
        }

        public async Task SaveAsync(string payload, bool isDefault)
        {
            using var scope = _sp.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<BestFlexDbContext>();

            var editor = new DbInvoiceTemplateProvider(db);
            editor.SetTemplateForCompany(_companyId, new PrintTemplate
            {
                Name = "Custom",
                Engine = "FlowDocument",
                Payload = payload,
                IsDefault = isDefault
            });

            await ReloadHistoryAsync();
        }

        public async Task RestoreVersionAsync(VersionItem item)
        {
            if (item == null) return;
            using var scope = _sp.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<BestFlexDbContext>();
            var editor = new DbInvoiceTemplateProvider(db);
            editor.SetTemplateForCompany(_companyId, new PrintTemplate
            {
                Name = "Restored",
                Engine = item.Engine,
                Payload = item.Payload,
                IsDefault = item.IsDefault
            });

            await ReloadHistoryAsync();
        }

        public FlowDocument PreviewDocument(string runtimePayload)
        {
            var engine = _sp.GetService(typeof(IInvoicePrintEngine)) as IInvoicePrintEngine;
            var runtimeTpl = new PrintTemplate
            {
                Name = "Preview",
                Engine = "FlowDocument",
                Payload = runtimePayload,
                IsDefault = false
            };

            var draft = SampleDraft();
            var ctx = SampleCompanyContext();

            return engine?.Render(draft, runtimeTpl, ctx) ?? new FlowDocument();
        }

        private BestFlex.Shell.Models.SaleDraft SampleDraft()
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

        public sealed class VersionItem
        {
            public int Id { get; set; }
            public string Label { get; set; } = "";
            public string Engine { get; set; } = "FlowDocument";
            public string Payload { get; set; } = "";
            public bool IsDefault { get; set; }
        }
    }
}
