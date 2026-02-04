using System;
using System.Collections.Generic;
using System.Linq;

namespace BestFlex.Shell.Configuration
{
    /// <summary>
    /// ERP v1 Capability Manifest - Defines what is officially supported in v1.0
    /// This is the single source of truth for feature availability
    /// </summary>
    public static class ErpCapabilityManifest
    {
        public const string Version = "1.0.0";
        public const string ReleaseName = "BestFlex ERP v1.0";
        
        /// <summary>
        /// Feature categories and their capabilities in v1.0
        /// </summary>
        public static readonly Dictionary<string, FeatureCategory> Capabilities = new()
        {
            ["Core"] = new FeatureCategory
            {
                Name = "Core",
                Status = FeatureStatus.ProductionReady,
                Features = new Dictionary<string, Feature>
                {
                    ["Login"] = new Feature { Name = "Login", Status = FeatureStatus.ProductionReady, Description = "User authentication and session management" },
                    ["Users"] = new Feature { Name = "Users", Status = FeatureStatus.ProductionReady, Description = "Basic user management" }
                }
            },
            
            ["Sales"] = new FeatureCategory
            {
                Name = "Sales",
                Status = FeatureStatus.ProductionReady,
                Features = new Dictionary<string, Feature>
                {
                    ["Customers"] = new Feature { Name = "Customers", Status = FeatureStatus.ProductionReady, Description = "Basic customer management" },
                    ["SalesOrders"] = new Feature { Name = "Sales Orders", Status = FeatureStatus.ProductionReady, Description = "Create and validate sales orders" },
                    ["Invoices"] = new Feature { Name = "Invoices", Status = FeatureStatus.ProductionReady, Description = "Post and view invoices" },
                    ["Statements"] = new Feature { Name = "Customer Statements", Status = FeatureStatus.ComingSoon, Description = "Customer account statements - v1.1+" }
                }
            },
            
            ["Inventory"] = new FeatureCategory
            {
                Name = "Inventory",
                Status = FeatureStatus.ProductionReady,
                Features = new Dictionary<string, Feature>
                {
                    ["Products"] = new Feature { Name = "Products", Status = FeatureStatus.ProductionReady, Description = "Basic product management" },
                    ["Visibility"] = new Feature { Name = "Inventory Visibility", Status = FeatureStatus.ProductionReady, Description = "Read-only inventory views" },
                    ["Receive"] = new Feature { Name = "Receive Stock", Status = FeatureStatus.InDevelopment, Description = "GRN and stock receiving - v1.1+" },
                    ["Adjust"] = new Feature { Name = "Stock Adjustments", Status = FeatureStatus.ComingSoon, Description = "Inventory adjustments - v1.2+" }
                }
            },
            
            ["Reporting"] = new FeatureCategory
            {
                Name = "Reporting",
                Status = FeatureStatus.ComingSoon,
                Features = new Dictionary<string, Feature>
                {
                    ["Dashboard"] = new Feature { Name = "Dashboard", Status = FeatureStatus.InDevelopment, Description = "Executive dashboard - v1.1+" },
                    ["Reports"] = new Feature { Name = "Reports", Status = FeatureStatus.ComingSoon, Description = "Financial and inventory reports - v1.2+" },
                    ["Analytics"] = new Feature { Name = "Analytics", Status = FeatureStatus.ComingSoon, Description = "Advanced analytics - v2.0+" }
                }
            },
            
            ["System"] = new FeatureCategory
            {
                Name = "System",
                Status = FeatureStatus.ProductionReady,
                Features = new Dictionary<string, Feature>
                {
                    ["Settings"] = new Feature { Name = "Settings", Status = FeatureStatus.ProductionReady, Description = "Basic system settings" },
                    ["Templates"] = new Feature { Name = "Templates", Status = FeatureStatus.InDevelopment, Description = "Document templates - v1.1+" },
                    ["Backup"] = new Feature { Name = "Backup", Status = FeatureStatus.ComingSoon, Description = "System backup - v1.2+" }
                }
            }
        };

        /// <summary>
        /// Route mappings for v1.0 features
        /// </summary>
        public static readonly Dictionary<string, RouteInfo> Routes = new()
        {
            // v1.0 Production Routes
            ["app://core/dashboard"] = new RouteInfo { Feature = "Dashboard", Category = "Reporting", Status = FeatureStatus.ProductionReady, PageType = "BestFlex.Shell.Pages.DashboardPage" },
            ["app://sales/new"] = new RouteInfo { Feature = "Sales Orders", Category = "Sales", Status = FeatureStatus.ProductionReady, PageType = "BestFlex.Shell.Pages.NewSalePage" },
            ["app://sales/invoices"] = new RouteInfo { Feature = "Invoices", Category = "Sales", Status = FeatureStatus.ProductionReady, PageType = "BestFlex.Shell.Pages.InvoicesPage" },
            ["app://inventory/products"] = new RouteInfo { Feature = "Products", Category = "Inventory", Status = FeatureStatus.ProductionReady, PageType = "BestFlex.Shell.Pages.ProductsPage" },
            
            // v1.1+ Development Routes
            ["app://inventory/receive"] = new RouteInfo { Feature = "Receive Stock", Category = "Inventory", Status = FeatureStatus.InDevelopment, PageType = "BestFlex.Shell.Views.Pages.Inventory.ReceiveStockPage" },
            ["app://core/templates"] = new RouteInfo { Feature = "Templates", Category = "System", Status = FeatureStatus.InDevelopment, PageType = "BestFlex.Shell.Pages.TemplateDesignerPage" },
            
            // Coming Soon Routes (Disabled)
            ["app://sales/statements"] = new RouteInfo { Feature = "Customer Statements", Category = "Sales", Status = FeatureStatus.ComingSoon, PageType = "BestFlex.Shell.Views.Pages.Sales.CustomerStatementsPage" }
        };

        /// <summary>
        /// Check if a feature is available in v1.0
        /// </summary>
        public static bool IsFeatureAvailable(string category, string feature)
        {
            if (!Capabilities.TryGetValue(category, out var featureCategory))
                return false;
                
            if (!featureCategory.Features.TryGetValue(feature, out var featureInfo))
                return false;
                
            return featureInfo.Status == FeatureStatus.ProductionReady;
        }

        /// <summary>
        /// Get feature status by route
        /// </summary>
        public static FeatureStatus GetRouteStatus(string route)
        {
            if (!Routes.TryGetValue(route, out var routeInfo))
                return FeatureStatus.ComingSoon;
                
            return routeInfo.Status;
        }

        /// <summary>
        /// Get all production-ready features
        /// </summary>
        public static IEnumerable<Feature> GetProductionFeatures()
        {
            return Capabilities.Values
                .SelectMany(c => c.Features.Values)
                .Where(f => f.Status == FeatureStatus.ProductionReady);
        }

        /// <summary>
        /// Get all development features
        /// </summary>
        public static IEnumerable<Feature> GetDevelopmentFeatures()
        {
            return Capabilities.Values
                .SelectMany(c => c.Features.Values)
                .Where(f => f.Status == FeatureStatus.InDevelopment);
        }

        /// <summary>
        /// Get all coming soon features
        /// </summary>
        public static IEnumerable<Feature> GetComingSoonFeatures()
        {
            return Capabilities.Values
                .SelectMany(c => c.Features.Values)
                .Where(f => f.Status == FeatureStatus.ComingSoon);
        }
    }

    public class FeatureCategory
    {
        public string Name { get; set; } = string.Empty;
        public FeatureStatus Status { get; set; }
        public Dictionary<string, Feature> Features { get; set; } = new();
    }

    public class Feature
    {
        public string Name { get; set; } = string.Empty;
        public FeatureStatus Status { get; set; }
        public string Description { get; set; } = string.Empty;
        public string? Version { get; set; }
    }

    public class RouteInfo
    {
        public string Feature { get; set; } = string.Empty;
        public string Category { get; set; } = string.Empty;
        public FeatureStatus Status { get; set; }
        public string PageType { get; set; } = string.Empty;
    }

    public enum FeatureStatus
    {
        ProductionReady,  // ✅ Available in v1.0
        InDevelopment,    // 🚧 In development (v1.1+)
        ComingSoon,       // ❌ Planned but not started
        Disabled          // ❌ Explicitly disabled
    }
}
