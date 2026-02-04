using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using BestFlex.Shell.Services;

namespace BestFlex.Shell.ViewModels
{
    public class MainWindowViewModel : INotifyPropertyChanged
    {
        private readonly IBusyService? _busyService;
        private string _title = "BestFlex ERP";
        private bool _isAdmin = true;
        private bool _isBusy = false;
        private string _busyMessage = string.Empty;
        private string _busyDetail = string.Empty;

        public string Title
        {
            get => _title;
            set => SetProperty(ref _title, value, nameof(Title));
        }

        public bool IsAdmin
        {
            get => _isAdmin;
            set => SetProperty(ref _isAdmin, value, nameof(IsAdmin));
        }

        public bool IsBusy
        {
            get => _isBusy;
            set => SetProperty(ref _isBusy, value, nameof(IsBusy));
        }

        public string BusyMessage
        {
            get => _busyMessage;
            set => SetProperty(ref _busyMessage, value, nameof(BusyMessage));
        }

        public string BusyDetail
        {
            get => _busyDetail;
            set => SetProperty(ref _busyDetail, value, nameof(BusyDetail));
        }

        public MainWindowViewModel()
        {
        }

        public MainWindowViewModel(IBusyService busyService)
        {
            _busyService = busyService ?? throw new ArgumentNullException(nameof(busyService));
            
            // Subscribe to busy state changes
            _busyService.BusyStateChanged += OnBusyStateChanged;
        }

        private void OnBusyStateChanged(object? sender, BusyStateChangedEventArgs e)
        {
            IsBusy = e.IsBusy;
            BusyMessage = e.Message;
            BusyDetail = e.Detail;
        }

        public async Task<string[]> GetUnavailableCoreFeatures()
        {
            // Return empty array - ERP must be resilient and never block startup
            // Features will be disabled in UI instead of blocking the entire application
            return await Task.FromResult(Array.Empty<string>());
        }

        public Task LoadAsync()
        {
            return Task.CompletedTask;
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
