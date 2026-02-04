namespace BestFlex.Application.Abstractions
{
    /// <summary>
    /// Sales module gate that enforces ERP v1.0 capability constraints
    /// </summary>
    public interface ISalesModuleGate
    {
        /// <summary>
        /// Check if Sales Order creation is allowed in current ERP version
        /// </summary>
        bool IsSalesOrderCreationEnabled();
        
        /// <summary>
        /// Check if Invoice posting is enabled in current ERP version
        /// </summary>
        bool IsInvoicePostingEnabled();
        
        /// <summary>
        /// Check if Customer Statements are available in current ERP version
        /// </summary>
        bool IsCustomerStatementEnabled();
        
        /// <summary>
        /// Legacy compatibility - overall sales module enabled status
        /// </summary>
        bool IsEnabled();
    }
}
