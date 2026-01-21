using System;
using System.Threading.Tasks;

namespace BestFlex.Application.Abstractions
{
    /// <summary>
    /// Core navigation service for ViewModels and business logic.
    /// Provides navigation to key business functions without UI dependencies.
    /// This interface is stable and should not be modified without architectural review.
    /// </summary>
    public interface INavigationService
    {
        /// <summary>Open invoice details window.</summary>
        void OpenInvoiceDetails(int invoiceId);
        
        /// <summary>Open customer account statement.</summary>
        void OpenAccountStatement(int customerId);
        
        /// <summary>Navigate to new sale page.</summary>
        void OpenNewSale();
        
        /// <summary>Open low stock window.</summary>
        void OpenLowStock(int threshold);
        
        /// <summary>Open unpaid invoices window.</summary>
        void OpenUnpaidInvoices(int topN, int? preselectCustomerId = null);
    }
}
