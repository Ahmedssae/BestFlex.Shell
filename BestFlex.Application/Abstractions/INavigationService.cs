using System;
using System.Threading.Tasks;

namespace BestFlex.Application.Abstractions
{
    /// <summary>
    /// Centralized navigation service surface used by ViewModels and code-behind.
    /// Implementations should create windows via DI and show them (Show/ShowDialog) as appropriate.
    /// </summary>
    public interface INavigationService
    {
        void OpenInvoiceDetails(int invoiceId);
        void OpenAccountStatement(int customerId);
        void OpenNewSale();
        void OpenLowStock(int threshold);
        void OpenUnpaidInvoices(int topN, int? preselectCustomerId = null);
    }
}
