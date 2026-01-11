using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using BestFlex.Application.Abstractions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace BestFlex.Shell.Infrastructure
{
    public class ModuleRegistry
    {
        private readonly IServiceProvider _sp;
        private readonly IConfiguration _cfg;
        private readonly List<IAppModule> _modules = new();

        public ModuleRegistry(IServiceProvider sp, IConfiguration cfg)
        { _sp = sp; _cfg = cfg; }

        public IReadOnlyList<IAppModule> Loaded => _modules;

        public async Task DiscoverAndLoadAsync()
        {
            var enabled = _cfg.GetSection("Modules:Enabled")
    .GetChildren()
    .Select(s => s.Value ?? string.Empty)
    .Where(v => !string.IsNullOrWhiteSpace(v))
    .ToArray();

            var set = enabled.Select(x => x.Trim().ToLowerInvariant()).ToHashSet();

            var candidates = _sp.GetServices<IAppModule>().OrderBy(m => m.Order).ToList();
            foreach (var m in candidates)
            {
                if (set.Count > 0 && !set.Contains(m.Key.ToLowerInvariant()))
                    continue;

                await m.InitializeAsync(_sp);
                _modules.Add(m);
            }
        }
    }
}
