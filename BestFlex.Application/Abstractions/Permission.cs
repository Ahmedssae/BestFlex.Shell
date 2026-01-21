using System;

namespace BestFlex.Application.Abstractions
{
    [Flags]
    public enum Permission
    {
        None = 0,
        
        // Sales permissions
        CreateSale = 1 << 0,
        ViewInvoices = 1 << 1,
        EditSale = 1 << 2,
        DeleteSale = 1 << 3,
        
        // Inventory permissions
        ReceiveStock = 1 << 4,
        AdjustStock = 1 << 5,
        ViewInventory = 1 << 6,
        
        // Accounting permissions
        PostAccounting = 1 << 7,
        ViewAccounting = 1 << 8,
        EditAccounting = 1 << 9,
        
        // Reporting permissions
        ViewReports = 1 << 10,
        ExportReports = 1 << 11,
        
        // System permissions
        PriceOverride = 1 << 12,
        ManageUsers = 1 << 13,
        ManageSettings = 1 << 14,
        
        // Admin permissions (all permissions)
        All = CreateSale | ViewInvoices | EditSale | DeleteSale |
              ReceiveStock | AdjustStock | ViewInventory |
              PostAccounting | ViewAccounting | EditAccounting |
              ViewReports | ExportReports |
              PriceOverride | ManageUsers | ManageSettings
    }
}
