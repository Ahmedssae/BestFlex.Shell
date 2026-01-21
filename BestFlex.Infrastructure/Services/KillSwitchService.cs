using System;
using Microsoft.Extensions.Configuration;
using BestFlex.Application.Abstractions;

namespace BestFlex.Infrastructure.Services
{
    public class KillSwitchService : IKillSwitchService
    {
        private readonly IConfiguration _config;

        public KillSwitchService(IConfiguration config)
        {
            _config = config ?? throw new ArgumentNullException(nameof(config));
        }

        public bool IsEnabled(KillSwitch killSwitch)
        {
            // Keys: KillSwitch:Sales, KillSwitch:Stock, KillSwitch:Accounting
            var key = killSwitch switch
            {
                KillSwitch.Sales => "KillSwitch:Sales",
                KillSwitch.Stock => "KillSwitch:Stock",
                KillSwitch.Accounting => "KillSwitch:Accounting",
                _ => null
            };

            if (key == null) return false;

            var val = _config[key];
            if (string.IsNullOrEmpty(val)) return false; // missing = disabled

            if (bool.TryParse(val, out var b)) return b;
            return false;
        }
    }
}
