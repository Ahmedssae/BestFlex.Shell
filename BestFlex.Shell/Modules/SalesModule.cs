using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using BestFlex.Application.Abstractions;

namespace BestFlex.Shell.Modules
{
    public class SalesModule : IAppModule
    {
        public string Key => "sales";
        public string DisplayName => "Sales";
        public int Order => 10;

        public IEnumerable<MenuItemDef> GetMenu() => new[]
        {
            new MenuItemDef("New Sale",  "open://sales/new",     "ShoppingCart", 1),
            new MenuItemDef("Invoices",  "open://sales/invoices","FileText",     2)
        };

        public Task InitializeAsync(IServiceProvider services) => Task.CompletedTask;
    }
}
