namespace BestFlex.Application.Abstractions
{
    public interface IPermissionService
    {
        bool CanViewSales();
        bool CanCreateSale();
        bool CanEditInvoice();
        bool CanViewReports();
        bool CanManageUsers();
        bool CanViewDashboard();
        bool CanViewInventory();
        bool CanManageSettings();
        bool CanViewDebt();
        bool CanViewLowStock();
        bool CanOpenInvoice();
    }
}
