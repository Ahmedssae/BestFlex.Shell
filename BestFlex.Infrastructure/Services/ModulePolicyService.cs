using System.Collections.Generic;
using BestFlex.Application.Abstractions;

namespace BestFlex.Infrastructure.Services
{
    public class ModulePolicyService : BestFlex.Application.Abstractions.IModulePolicyService
    {
        // Deterministic hard-coded policy for Phase 14. No DB, no shell references.
        private static readonly HashSet<ErpModule> EnabledModules = new()
        {
            ErpModule.Inventory,
            ErpModule.Accounting,
            ErpModule.Reports,
            ErpModule.Printing,
            ErpModule.Templates
            // Sales intentionally excluded for now
        };

        public bool IsEnabled(ErpModule module)
        {
            return EnabledModules.Contains(module);
        }

        public void ValidateEnabled(ErpModule module)
        {
            if (!IsEnabled(module))
            {
                throw new BestFlex.Application.Abstractions.UserFriendlyException($"Module disabled: {module}");
            }
        }
    }
}
