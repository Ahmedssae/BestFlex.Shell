using System.Linq;
using BestFlex.Application.Abstractions;

namespace BestFlex.Infrastructure.Services
{
    public sealed class PermissionService : IPermissionService
    {
        private readonly ICurrentUserService _currentUser;

        public PermissionService(ICurrentUserService currentUser)
        {
            _currentUser = currentUser ?? throw new System.ArgumentNullException(nameof(currentUser));
        }

        private bool HasRole(string role) => _currentUser.Roles.Contains(role, System.StringComparer.OrdinalIgnoreCase);
        private bool IsAdmin() => HasRole("Admin") || HasRole("Administrator");

        public bool CanViewSales() => IsAdmin() || HasRole("Sales") || HasRole("SalesUser");
        public bool CanCreateSale() => IsAdmin() || HasRole("Sales") || HasRole("SalesUser");
        public bool CanEditInvoice() => IsAdmin() || HasRole("Sales") || HasRole("Accountant");
        public bool CanViewReports() => IsAdmin() || HasRole("Manager") || HasRole("Accountant");
        public bool CanManageUsers() => IsAdmin();
        public bool CanViewDashboard() => IsAdmin() || HasRole("Manager") || HasRole("Sales") || HasRole("SalesUser");
        public bool CanViewInventory() => IsAdmin() || HasRole("Inventory") || HasRole("Manager");
        public bool CanManageSettings() => IsAdmin();
        public bool CanViewDebt() => IsAdmin() || HasRole("Manager") || HasRole("Accountant");
        public bool CanViewLowStock() => IsAdmin() || HasRole("Inventory") || HasRole("Manager");
        public bool CanOpenInvoice() => IsAdmin() || HasRole("Sales") || HasRole("SalesUser") || HasRole("Accountant");
    }
}
