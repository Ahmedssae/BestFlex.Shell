using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace BestFlex.Shell.ViewModels
{
    public class DashboardItemDto
    {
        public int Id { get; set; }
        public string Title { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public decimal Value { get; set; }
        public string Type { get; set; } = string.Empty; // Sales, Inventory, Customer, etc.
        public DateTime LastUpdated { get; set; }
        public bool IsAvailable { get; set; } = true; // Whether this feature is available in current version
        public string Status { get; set; } = string.Empty; // "Available", "Coming Soon", "In Development"
    }

    public class DashboardViewModel : INotifyPropertyChanged
    {
        private ObservableCollection<DashboardItemDto> _items = new();
        private bool _isLoading = false;
        private string _errorMessage = string.Empty;

        public ObservableCollection<DashboardItemDto> Items
        {
            get => _items;
            set => SetProperty(ref _items, value, nameof(Items));
        }

        public bool IsLoading
        {
            get => _isLoading;
            set => SetProperty(ref _isLoading, value, nameof(IsLoading));
        }

        public string ErrorMessage
        {
            get => _errorMessage;
            set => SetProperty(ref _errorMessage, value, nameof(ErrorMessage));
        }

        public bool HasData => Items.Count > 0;

        public DashboardViewModel()
        {
            // ERP REQUIREMENT: Dashboard is ALWAYS available, never gated
            // Load static capability information without requiring any services
            LoadCapabilityBasedItems();
        }

        private void LoadCapabilityBasedItems()
        {
            try
            {
                IsLoading = true;
                ErrorMessage = string.Empty;
                Items.Clear();

                // Add dashboard items based on ERP v1.0 capabilities
                // This is static information - no service dependencies required
                
                // Sales features available in v1.0
                Items.Add(new DashboardItemDto
                {
                    Id = 1,
                    Title = "Sales Orders",
                    Description = "Create and manage sales orders",
                    Type = "Sales",
                    LastUpdated = DateTime.Now,
                    IsAvailable = true,
                    Status = "Available"
                });

                Items.Add(new DashboardItemDto
                {
                    Id = 2,
                    Title = "Invoices",
                    Description = "Post and view invoices",
                    Type = "Sales",
                    LastUpdated = DateTime.Now,
                    IsAvailable = true,
                    Status = "Available"
                });

                // Features coming in v1.1+
                Items.Add(new DashboardItemDto
                {
                    Id = 3,
                    Title = "Customer Statements",
                    Description = "Generate customer account statements",
                    Type = "Sales",
                    LastUpdated = DateTime.Now,
                    IsAvailable = false,
                    Status = "Coming Soon (v1.1+)"
                });

                // Inventory features
                Items.Add(new DashboardItemDto
                {
                    Id = 4,
                    Title = "Inventory Visibility",
                    Description = "View current inventory levels",
                    Type = "Inventory",
                    LastUpdated = DateTime.Now,
                    IsAvailable = true,
                    Status = "Available"
                });

                Items.Add(new DashboardItemDto
                {
                    Id = 5,
                    Title = "Receive Stock (GRN)",
                    Description = "Goods Receipt Note processing",
                    Type = "Inventory",
                    LastUpdated = DateTime.Now,
                    IsAvailable = false,
                    Status = "In Development (v1.1+)"
                });

                // Core features
                Items.Add(new DashboardItemDto
                {
                    Id = 6,
                    Title = "Templates",
                    Description = "Document template designer",
                    Type = "Core",
                    LastUpdated = DateTime.Now,
                    IsAvailable = false,
                    Status = "In Development (v1.1+)"
                });
            }
            catch
            {
                ErrorMessage = "Failed to load dashboard information.";
            }
            finally
            {
                IsLoading = false;
            }
        }

        public async Task LoadAsync()
        {
            try
            {
                IsLoading = true;
                ErrorMessage = string.Empty;

                // Simulate async operation to avoid warning
                await Task.Delay(10);

                // Dashboard already loaded with capability-based items in constructor
                // This method is kept for compatibility but doesn't need to do anything
            }
            catch
            {
                ErrorMessage = "Failed to load dashboard data. Please try again.";
            }
            finally
            {
                IsLoading = false;
            }
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
