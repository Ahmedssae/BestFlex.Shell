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

        public SafeFallbackPage(string errorMessage) : this()
        {
            ErrorMessage = errorMessage ?? "Unknown error occurred.";
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
