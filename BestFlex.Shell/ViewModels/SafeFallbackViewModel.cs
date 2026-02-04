using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using BestFlex.Shell.Abstractions;

namespace BestFlex.Shell.ViewModels
{
    public class SafeFallbackViewModel : INotifyPropertyChanged
    {
        private string _message = "Feature not available in Phase 7A";
        private string _failedRoute = string.Empty;

        public string Message
        {
            get => _message;
            set => SetProperty(ref _message, value, nameof(Message));
        }

        public string FailedRoute
        {
            get => _failedRoute;
            set => SetProperty(ref _failedRoute, value, nameof(FailedRoute));
        }

        public SafeFallbackViewModel()
        {
        }

        public SafeFallbackViewModel(string failedRoute)
        {
            FailedRoute = failedRoute;
            Message = GetRouteAwareMessage(failedRoute);
        }

        public SafeFallbackViewModel(string message, string title, string description, string action)
        {
            Message = message;
        }

        public SafeFallbackViewModel(ILogger<SafeFallbackViewModel> logger, IServiceProvider serviceProvider, IShellNavigationService navigationService)
        {
            Message = "Feature not available in Phase 7A";
        }

        private string GetRouteAwareMessage(string failedRoute)
        {
            return failedRoute switch
            {
                "app://sales/new" => "⚠️ New Sale is temporarily unavailable\n\nThe Sales Order Entry screen is currently under reconstruction.\n\nCore domain logic is active. UI is being rebuilt.\n\nPlease try again later or contact support if the problem persists.",
                "app://sales/invoices" => "⚠️ Invoices could not be loaded\n\nThe Invoices screen is currently unavailable.\n\nPlease try again later or contact support if the problem persists.",
                "app://core/dashboard" => "⚠️ Dashboard could not be loaded\n\nThe Dashboard screen is currently unavailable.\n\nPlease try again later or contact support if the problem persists.",
                _ => $"⚠️ Feature Unavailable\n\n{GetFeatureName(failedRoute)} is currently unavailable due to a technical issue.\n\nPlease try again later or contact support if the problem persists."
            };
        }

        private static string GetFeatureName(string route)
        {
            return route switch
            {
                "app://core/dashboard" => "Dashboard",
                "app://sales/new" => "Sales Orders",
                "app://sales/invoices" => "Invoices",
                "app://sales/statements" => "Customer Statements",
                "app://inventory/receive" => "Stock Receiving",
                "app://core/templates" => "Template Designer",
                _ => "Requested Feature"
            };
        }

        public event PropertyChangedEventHandler? PropertyChanged;

        protected virtual void OnPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        protected bool SetProperty<T>(ref T field, T value, string propertyName)
        {
            if (Equals(field, value)) return false;
            field = value;
            OnPropertyChanged(propertyName);
            return true;
        }
    }
}
