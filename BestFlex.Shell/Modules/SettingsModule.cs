using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using BestFlex.Application.Abstractions;

namespace BestFlex.Shell.Modules
{
    public class SettingsModule : IAppModule
    {
        public string Key => "settings";
        public string DisplayName => "Settings";
        public int Order => 90;

        public IEnumerable<MenuItemDef> GetMenu() => new[]
        {
            new MenuItemDef("App Settings", "open://settings", "Settings", 1)
        };

        public Task InitializeAsync(IServiceProvider services) => Task.CompletedTask;
    }
}
