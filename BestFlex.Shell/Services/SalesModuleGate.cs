using BestFlex.Application.Abstractions;
using BestFlex.Shell.Configuration;

namespace BestFlex.Shell.Services
{
    /// <summary>
    /// Sales module gate that enforces ERP v1.0 capability constraints
    /// </summary>
    public sealed class SalesModuleGate : ISalesModuleGate
    {
        private readonly ICapabilityService _capabilityService;

        public SalesModuleGate(ICapabilityService capabilityService)
        {
            _capabilityService = capabilityService ?? throw new ArgumentNullException(nameof(capabilityService));
        }

        public bool IsSalesOrderCreationEnabled()
        {
            // Sales Orders are available in ERP v1.0
            return _capabilityService.IsFeatureAvailable("Sales", "Sales Orders");
        }

        public bool IsInvoicePostingEnabled()
        {
            // Invoices are available in ERP v1.0
            return _capabilityService.IsFeatureAvailable("Sales", "Invoices");
        }

        public bool IsCustomerStatementEnabled()
        {
            // Customer Statements are coming in v1.1+, not available in v1.0
            return _capabilityService.IsFeatureAvailable("Sales", "Customer Statements");
        }

        public bool IsEnabled()
        {
            // Sales module is enabled if any sales feature is available
            return IsSalesOrderCreationEnabled() || IsInvoicePostingEnabled() || IsCustomerStatementEnabled();
        }
    }
}
