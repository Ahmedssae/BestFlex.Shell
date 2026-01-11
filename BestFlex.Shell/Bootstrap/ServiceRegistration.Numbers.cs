using BestFlex.Shell.Services;
using Microsoft.Extensions.DependencyInjection;

namespace BestFlex.Shell.Bootstrap
{
    public static class ServiceRegistrationNumbers
    {
        public static IServiceCollection AddInvoiceNumbering(this IServiceCollection services)
        {
            services.AddScoped<IInvoiceNumberService, EfInvoiceNumberService>();
            return services;
        }
    }
}
