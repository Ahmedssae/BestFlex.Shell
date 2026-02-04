using System;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Controls;

namespace BestFlex.Shell.Pages
{
    public partial class SafeFallbackPage : UserControl, INotifyPropertyChanged
    {
        private string _errorMessage = "The requested page could not be loaded.";

        public string ErrorMessage
        {
            get => _errorMessage;
            set { if (_errorMessage != value) { _errorMessage = value; OnPropertyChanged(); } }
        }

        public SafeFallbackPage()
        {
            InitializeComponent();
            DataContext = this;
        }

        public SafeFallbackPage(string route) : this()
        {
#if DEBUG
            // In DEBUG builds, show the full route/error information for debugging
            ErrorMessage = route switch
            {
                "app://sales/new" =>
                    "Sales Order Entry is under construction.\nThis screen will be available soon.\n\nDEBUG: Route = " + route,

                "app://sales/invoices" =>
                    "Invoice module failed to load.\n\nDEBUG: Route = " + route + "\n\nCheck logs for full exception details.",

                "app://core/dashboard" =>
                    "Dashboard failed to load.\n\nDEBUG: Route = " + route,

                _ =>
                    $"The requested screen is unavailable:\n{route}\n\nDEBUG: Full error details above"
            };
#else
            // In RELEASE builds, show user-friendly messages
            ErrorMessage = route switch
            {
                "app://sales/new" =>
                    "Sales Order Entry is under construction.\nThis screen will be available soon.",

                "app://sales/invoices" =>
                    "Invoice module is temporarily unavailable.",

                "app://core/dashboard" =>
                    "Dashboard failed to load.",

                _ =>
                    $"The requested screen is unavailable:\n{route}"
            };
#endif
        }

        private void ReturnToDashboard_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                // Close the current window to return to main shell
                var parentWindow = Window.GetWindow(this);
                parentWindow?.Close();
            }
            catch
            {
                // If closing fails, do nothing to avoid crash
            }
        }

        public event PropertyChangedEventHandler? PropertyChanged;
        private void OnPropertyChanged([CallerMemberName] string? name = null)
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }
}
