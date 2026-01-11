using System.Windows.Documents;
using BestFlex.Shell.Models;

namespace BestFlex.Shell.Printing
{
    public interface IInvoicePrintEngine
    {
        FlowDocument Render(SaleDraft draft, PrintTemplate tpl, CompanyPrintContext ctx);
    }

    public sealed class CompanyPrintContext
    {
        public int CompanyId { get; set; } = 1;
        public string? CompanyName { get; set; }
        public string? CompanyPhone { get; set; }
        public string? CompanyAddress { get; set; }
        public string? FooterNote { get; set; } = "Thank you for your business.";
    }
}
