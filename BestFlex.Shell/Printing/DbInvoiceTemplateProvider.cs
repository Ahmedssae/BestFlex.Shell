using System;
using System.Linq;
using BestFlex.Persistence.Data;
using Microsoft.EntityFrameworkCore;
using DbPrintTemplate = BestFlex.Domain.Entities.PrintTemplate;
using DbTemplateVersion = BestFlex.Domain.Entities.PrintTemplateVersion;
using ShellTemplate = BestFlex.Shell.Printing.PrintTemplate;

namespace BestFlex.Shell.Printing
{
    public sealed class DbInvoiceTemplateProvider : IInvoiceTemplateProvider, IInvoiceTemplateEditor
    {
        private readonly BestFlexDbContext _db;
        public DbInvoiceTemplateProvider(BestFlexDbContext db) => _db = db;

        public ShellTemplate GetTemplateForCompany(int companyId) => GetTemplateForCompany(companyId, "invoice");

        public ShellTemplate GetTemplateForCompany(int companyId, string docType)
        {
            var rec = _db.PrintTemplates.AsNoTracking()
                        .Where(t => t.CompanyId == companyId && t.DocType == docType)
                        .OrderByDescending(t => !t.IsDefault)
                        .ThenByDescending(t => t.Id)
                        .FirstOrDefault();

            if (rec != null)
            {
                return new ShellTemplate
                {
                    Name = rec.IsDefault ? "Default (DB)" : "Custom (DB)",
                    Engine = rec.Engine,
                    Payload = rec.Payload,
                    IsDefault = rec.IsDefault
                };
            }

            return new InMemoryInvoiceTemplateProvider().GetTemplateForCompany(companyId);
        }

        public void SetTemplateForCompany(int companyId, ShellTemplate template, string docType = "invoice")
        {
            var engine = string.IsNullOrWhiteSpace(template.Engine) ? "FlowDocument" : template.Engine;
            var payload = template.Payload ?? string.Empty;
            var makeDefault = template.IsDefault;

            // 1) If setting default, clear existing default
            if (makeDefault)
            {
                var others = _db.PrintTemplates
                    .Where(t => t.CompanyId == companyId && t.DocType == docType && t.IsDefault);
                foreach (var o in others) o.IsDefault = false;
            }

            // 2) Upsert the current row (custom or default)
            var custom = _db.PrintTemplates
                            .FirstOrDefault(t => t.CompanyId == companyId && t.DocType == docType && !t.IsDefault);
            var currentDefault = _db.PrintTemplates
                            .FirstOrDefault(t => t.CompanyId == companyId && t.DocType == docType && t.IsDefault);

            if (makeDefault)
            {
                if (currentDefault == null)
                {
                    _db.PrintTemplates.Add(new DbPrintTemplate
                    {
                        CompanyId = companyId,
                        DocType = docType,
                        Engine = engine,
                        Payload = payload,
                        IsDefault = true
                    });
                }
                else
                {
                    currentDefault.Engine = engine;
                    currentDefault.Payload = payload;
                    currentDefault.IsDefault = true;
                    _db.PrintTemplates.Update(currentDefault);
                }
            }
            else
            {
                if (custom == null)
                {
                    _db.PrintTemplates.Add(new DbPrintTemplate
                    {
                        CompanyId = companyId,
                        DocType = docType,
                        Engine = engine,
                        Payload = payload,
                        IsDefault = false
                    });
                }
                else
                {
                    custom.Engine = engine;
                    custom.Payload = payload;
                    _db.PrintTemplates.Update(custom);
                }
            }

            // 3) Always append a version snapshot (immutable)
            _db.PrintTemplateVersions.Add(new DbTemplateVersion
            {
                CompanyId = companyId,
                DocType = docType,
                Engine = engine,
                Payload = payload,
                IsDefault = makeDefault,
                CreatedAtUtc = DateTime.UtcNow,
                CreatedBy = "Designer" // replace with current user when available
            });

            _db.SaveChanges();
        }
    }
}
