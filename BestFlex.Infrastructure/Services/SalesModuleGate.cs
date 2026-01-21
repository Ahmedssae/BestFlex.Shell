using BestFlex.Application.Abstractions;

namespace BestFlex.Infrastructure.Services
{
    public sealed class SalesModuleGate : ISalesModuleGate
    {
        public bool IsEnabled() => false; // Phase 12: sales locked off
    }
}
