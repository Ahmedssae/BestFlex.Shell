using System;

namespace BestFlex.Application.Abstractions
{
    public enum ErpModule
    {
        Sales,
        Inventory,
        Accounting,
        Reports,
        Printing,
        Templates
    }

    public interface IModulePolicyService
    {
        bool IsEnabled(ErpModule module);

        /// <summary>
        /// Throws <see cref="UserFriendlyException"/> if the module is disabled.
        /// </summary>
        /// <param name="module"></param>
        void ValidateEnabled(ErpModule module);
    }
}
