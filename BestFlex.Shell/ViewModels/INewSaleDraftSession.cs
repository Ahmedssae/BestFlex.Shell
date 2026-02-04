using BestFlex.Shell.Models;

namespace BestFlex.Shell.ViewModels
{
    public interface INewSaleDraftSession
    {
        SalesOrderDraft Draft { get; }
        int? PersistedOrderId { get; set; }
        bool IsPosted { get; set; }
        void Reset();
    }
}
