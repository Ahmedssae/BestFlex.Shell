using System;
using System.Threading.Tasks;
using System.Windows;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using BestFlex.Shell.UI.Toasts;

namespace BestFlex.Shell.Bootstrap
{
    public static class GlobalExceptionHook
    {
        public static void Wire(IServiceProvider sp)
        {
            var logger = sp.GetService<ILoggerFactory>()?.CreateLogger("GlobalExceptions");
            var toast = sp.GetService<IToastService>();

            void Handle(Exception ex, string source)
            {
                logger?.LogError(ex, "Unhandled ({Source}) {Message}", source, ex.Message);
                toast?.Show("Unexpected error — details saved to logs.");
            }

            AppDomain.CurrentDomain.UnhandledException += (_, e) =>
            {
                if (e.ExceptionObject is Exception ex) Handle(ex, "AppDomain");
            };

            if (System.Windows.Application.Current != null)
            {
                System.Windows.Application.Current.DispatcherUnhandledException += (_, e) =>
                {
                    Handle(e.Exception, "Dispatcher");
                    e.Handled = true; // avoid crash if possible
                };
            }

            TaskScheduler.UnobservedTaskException += (_, e) =>
            {
                Handle(e.Exception, "TaskScheduler");
                e.SetObserved();
            };
        }
    }
}
