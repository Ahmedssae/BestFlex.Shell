using BestFlex.Application.Abstractions;
using BestFlex.Shell.Infrastructure;
using BestFlex.Shell.Navigation;
using BestFlex.Shell.Views.Pages.Inventory;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Windows.Controls;

namespace BestFlex.Shell.Modules
{
    public sealed class InventoryModule : IAppModule
    {
        public string Key => "inventory";
        public string DisplayName => "Inventory";
        public int Order => 30;

        public IEnumerable<MenuItemDef> GetMenu() => new[]
        {
            // Remove named parameter 'order' (not supported in your ctor overload)
            new MenuItemDef("Receive (GRN)", "app://inventory/receive")
        };

        public Task InitializeAsync(IServiceProvider services)
        {
            var nav = services.GetRequiredService<INavigator>();
            // Navigator expects Func<UserControl>; return the page as UserControl
            nav.Register("app://inventory/receive",
                () => (UserControl)services.GetRequiredService<ReceiveStockPage>());
            return Task.CompletedTask;
        }
    }
}
