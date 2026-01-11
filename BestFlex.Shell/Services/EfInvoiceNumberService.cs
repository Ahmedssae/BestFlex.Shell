using System;
using System.Linq;
using System.Threading.Tasks;
using BestFlex.Domain.Entities;
using BestFlex.Persistence.Data;
using Microsoft.EntityFrameworkCore;

namespace BestFlex.Shell.Services
{
    public sealed class EfInvoiceNumberService : IInvoiceNumberService
    {
        private readonly BestFlexDbContext _db;
        public EfInvoiceNumberService(BestFlexDbContext db) => _db = db;

        public async Task<string> NextAsync(int companyId)
        {
            var ym = DateTime.UtcNow.ToString("yyyyMM");

            for (var attempt = 0; attempt < 6; attempt++)
            {
                var seq = await _db.InvoiceNoSequences
                    .SingleOrDefaultAsync(s => s.CompanyId == companyId && s.YearMonth == ym);

                if (seq == null)
                {
                    seq = new InvoiceNoSequence { CompanyId = companyId, YearMonth = ym, Next = 1 };
                    _db.InvoiceNoSequences.Add(seq);
                }

                var number = seq.Next;
                seq.Next++;

                try
                {
                    await _db.SaveChangesAsync();
                    return $"INV-{ym}-{number:00000}";
                }
                catch (DbUpdateConcurrencyException)
                {
                    // Someone else updated it; retry a few times.
                    foreach (var entry in _db.ChangeTracker.Entries<InvoiceNoSequence>())
                        entry.State = EntityState.Detached;
                    continue;
                }
            }

            throw new InvalidOperationException("Failed to allocate an invoice number.");
        }
    }
}
