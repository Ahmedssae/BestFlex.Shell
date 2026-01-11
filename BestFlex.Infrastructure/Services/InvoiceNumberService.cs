using System.Linq;
using System.Threading.Tasks;
using BestFlex.Persistence.Data;
using Microsoft.EntityFrameworkCore;

namespace BestFlex.Infrastructure.Services
{
    public class InvoiceNumberService
    {
        private readonly BestFlexDbContext _db;
        public InvoiceNumberService(BestFlexDbContext db) => _db = db;

        /// <summary>
        /// Generates the next invoice number like: "INVS 0001"
        /// Uses the highest existing number that starts with the same prefix.
        /// </summary>
        public async Task<string> NextAsync(string prefix = "INVS ")
        {
            var last = await _db.SellingInvoices
                .Where(x => x.InvoiceNo.StartsWith(prefix))
                .OrderByDescending(x => x.InvoiceNo)
                .Select(x => x.InvoiceNo)
                .FirstOrDefaultAsync();

            int n = 0;
            if (!string.IsNullOrWhiteSpace(last) && last.Length > prefix.Length)
            {
                var tail = last.Substring(prefix.Length).Trim();
                _ = int.TryParse(tail, out n);
            }
            return $"{prefix}{(n + 1):D4}";
        }
    }
}
