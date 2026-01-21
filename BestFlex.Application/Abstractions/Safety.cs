using System;

namespace BestFlex.Application.Abstractions
{
    public enum RuntimeEnvironment
    {
        Development,
        Production
    }

    public interface IEnvironmentContext
    {
        RuntimeEnvironment Current { get; }
    }

    public enum KillSwitch
    {
        Sales,
        Stock,
        Accounting
    }

    public interface IKillSwitchService
    {
        bool IsEnabled(KillSwitch killSwitch);
    }

    public interface ISystemSafetyPolicy
    {
        /// <summary>
        /// Ensure the requested operation is allowed under current environment and kill switches.
        /// Throws <see cref="UserFriendlyException"/> when blocked in Production.
        /// </summary>
        void EnsureOperationAllowed(KillSwitch killSwitch, string operationName);
    }
}
