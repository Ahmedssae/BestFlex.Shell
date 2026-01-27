using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Globalization;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using BestFlex.Infrastructure.Services;
using BestFlex.Persistence.Data;
using Microsoft.Extensions.DependencyInjection;
using BestFlex.Shell.Infrastructure;

namespace BestFlex.Shell.Pages
{
    public partial class DashboardPage : UserControl, INotifyPropertyChanged
    {
        private readonly ViewModels.DashboardViewModel _vm;

        // Theme label text bound from XAML
        private string _themeText = "Light";
        public string ThemeText
        {
            get => _themeText;
            set { if (_themeText != value) { _themeText = value; OnPropertyChanged(); } }
        }

        public DashboardPage(ViewModels.DashboardViewModel vm)
        {
            InitializeComponent();
            _vm = vm ?? throw new ArgumentNullException(nameof(vm));
            
            // Set DataContext to the dashboard view model. ThemeText in XAML uses RelativeSource to the UserControl
            // so it remains available from the code-behind even when DataContext is the VM.
            DataContext = _vm;
            ThemeText = UserPrefs.Current.Theme == "Dark" ? "Dark" : "Light";
        }

        // Events
        private async void UserControl_Loaded(object sender, RoutedEventArgs e)
        {
            // Simplified dashboard - just basic loading
            await Task.CompletedTask;
        }

        private void btnTheme_Click(object sender, RoutedEventArgs e)
        {
            ThemeManager.Toggle();
            ThemeText = UserPrefs.Current.Theme == "Dark" ? "Dark" : "Light";
        }

        // ---------- INotifyPropertyChanged ----------
        public event PropertyChangedEventHandler? PropertyChanged;
        private void OnPropertyChanged([CallerMemberName] string? name = null)
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }
}
