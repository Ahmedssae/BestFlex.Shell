using System.Windows.Documents;
using BestFlex.Application.Abstractions.Inventory;

namespace BestFlex.Shell.Printing
{
    public interface IGrnPrintEngine
    {
        FlowDocument CreateGrnDocument(ReceiveDraft draft, PurchaseReceiptResult result);
    }
}
