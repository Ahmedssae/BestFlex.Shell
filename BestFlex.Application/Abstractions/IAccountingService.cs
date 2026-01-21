using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using BestFlex.Domain.Entities;

namespace BestFlex.Application.Abstractions
{
    public interface IAccountingService
    {
        Task PostInvoiceAsync(SellingInvoice invoice, CancellationToken cancellationToken = default);
        Task<bool> ValidateBalancedEntry(IEnumerable<JournalLine> journalLines, CancellationToken cancellationToken = default);
    }
}
