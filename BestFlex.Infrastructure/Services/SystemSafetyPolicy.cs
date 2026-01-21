using BestFlex.Application.Abstractions;

namespace BestFlex.Infrastructure.Services
{
    public class SystemSafetyPolicy : ISystemSafetyPolicy
    {
        private readonly IEnvironmentContext _env;
        private readonly IKillSwitchService _ks;

        public SystemSafetyPolicy(IEnvironmentContext env, IKillSwitchService ks)
        {
            _env = env;
            _ks = ks;
        }

        public void EnsureOperationAllowed(KillSwitch killSwitch, string operationName)
        {
            // Deterministic policy: only block when Production and kill switch disabled
            if (_env.Current == RuntimeEnvironment.Production)
            {
                var enabled = _ks.IsEnabled(killSwitch);
                if (!enabled)
                {
                    throw new BestFlex.Application.Abstractions.UserFriendlyException($"{operationName} is temporarily disabled for system safety. Please contact your administrator.");
                }
            }
            // In Development environment, always allowed
        }
    }
}
