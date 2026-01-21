using System;

namespace BestFlex.Shell.Abstractions
{
    /// <summary>
    /// Central navigation route registry for the ERP system
    /// </summary>
    public static class NavigationRoutes
    {
        // Core routes - must be available
        public const string Dashboard = "app://core/dashboard";
        public const string NewSale = "app://sales/new";
        public const string Invoices = "app://sales/invoices";
        public const string Customers = "app://sales/customers";
        public const string CustomerStatements = "app://sales/statements";
        public const string AccountStatement = "app://sales/account-statement";
        public const string Products = "app://sales/products";
        public const string Reports = "app://sales/reports";
        
        // Optional routes - may be unavailable
        public const string Settings = "app://core/settings";
        public const string TemplateDesigner = "app://core/templates";
        public const string LowStock = "app://inventory/low-stock";
        public const string UnpaidInvoices = "app://sales/unpaid";
        public const string ReceiveStock = "app://inventory/receive";
        public const string GrnPreview = "app://inventory/grn-preview";
        
        // Feature requirements for routes
        public static class FeatureRequirements
        {
            public static readonly string[] Dashboard = { "Navigation", "Sales" };
            public static readonly string[] NewSale = { "Navigation", "Sales", "ProductLookup", "CustomerLookup" };
            public static readonly string[] Invoices = { "Navigation", "Sales" };
            public static readonly string[] Customers = { "Navigation", "CustomerLookup" };
            public static readonly string[] CustomerStatements = { "Navigation", "CustomerLookup", "Reports" };
            public static readonly string[] AccountStatement = { "Navigation", "CustomerLookup", "Reports" };
            public static readonly string[] Products = { "Navigation", "ProductLookup" };
            public static readonly string[] Reports = { "Navigation", "Reports" };
            
            public static readonly string[] Settings = { "Navigation", "Settings" };
            public static readonly string[] TemplateDesigner = { "Navigation", "TemplateDesigner" };
            public static readonly string[] LowStock = { "Navigation", "ProductLookup" };
            public static readonly string[] UnpaidInvoices = { "Navigation", "Sales" };
            public static readonly string[] ReceiveStock = { "Navigation", "ProductLookup" };
            public static readonly string[] GrnPreview = { "Navigation", "Printing" };
        }
    }
}
