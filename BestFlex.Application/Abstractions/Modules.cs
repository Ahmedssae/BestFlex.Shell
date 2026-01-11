using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace BestFlex.Application.Abstractions
{
    public sealed record MenuItemDef(string Title, string Route, string? Icon = null, int Order = 0);

    public interface IAppModule
    {
        string Key { get; }           // "sales", "settings", "warehouse"...
        string DisplayName { get; }   // Sidebar header
        int Order { get; }            // Sidebar grouping order
        IEnumerable<MenuItemDef> GetMenu();
        Task InitializeAsync(IServiceProvider services);
    }
}
