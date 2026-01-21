using System;
using BestFlex.Application.Abstractions;

namespace BestFlex.Infrastructure.Services
{
    public class EnvironmentContext : IEnvironmentContext
    {
        public RuntimeEnvironment Current { get; }

        public EnvironmentContext()
        {
            var env = Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT")
                      ?? Environment.GetEnvironmentVariable("DOTNET_ENVIRONMENT");

            if (string.Equals(env, "Development", StringComparison.OrdinalIgnoreCase))
                Current = RuntimeEnvironment.Development;
            else
                Current = RuntimeEnvironment.Production;
        }
    }
}
