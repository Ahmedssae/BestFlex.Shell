using System;
using System.IO;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using BestFlex.Shell.Logging;

namespace BestFlex.Shell.Bootstrap
{
    public static class ServiceRegistrationLogging
    {
        public static IServiceCollection AddFileLogging(this IServiceCollection services, string? directory = null)
        {
            directory ??= Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "BestFlex", "logs");

            services.AddLogging(b =>
            {
                b.ClearProviders();
                b.AddProvider(new FileLoggerProvider(directory));
                b.SetMinimumLevel(LogLevel.Information);
            });

            return services;
        }
    }
}
