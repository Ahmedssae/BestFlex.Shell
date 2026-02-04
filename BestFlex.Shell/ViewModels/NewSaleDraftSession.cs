using BestFlex.Shell.Models;

namespace BestFlex.Shell.ViewModels
{
    public class NewSaleDraftSession : INewSaleDraftSession
    {
        public SalesOrderDraft Draft { get; }

        public NewSaleDraftSession()
        {
            Draft = new SalesOrderDraft();
        }

        public int? PersistedOrderId { get; set; }
        public bool IsPosted { get; set; }

        public void Reset()
        {
            Draft.CustomerName = string.Empty;
            Draft.Lines.Clear();
            PersistedOrderId = null;
            IsPosted = false;
        }
    }
}
