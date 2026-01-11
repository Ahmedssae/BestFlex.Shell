using BestFlex.Shell.UI.Toasts;
using Microsoft.Extensions.DependencyInjection;

namespace BestFlex.Shell.Bootstrap
{
    public static class ServiceRegistrationUI
    {
        public static IServiceCollection AddUiHelpers(this IServiceCollection services)
        {
            services.AddSingleton<IToastService, ToastService>();
            return services;
        }
    }
}
