using BestFlex.Shell.Printing;
using Microsoft.Extensions.DependencyInjection;

namespace BestFlex.Shell.Bootstrap
{
    public static class ServiceRegistrationPrinting
    {
        /// <summary>
        /// Use DB-backed invoice templates app-wide.
        /// NOTE: Scoped lifetime (provider needs scoped DbContext).
        /// </summary>
        public static IServiceCollection AddDbBackedInvoiceTemplates(this IServiceCollection services)
        {
            services.AddScoped<IInvoiceTemplateProvider, DbInvoiceTemplateProvider>(); // FIX: Scoped, not Singleton
            return services;
        }
    }
}
