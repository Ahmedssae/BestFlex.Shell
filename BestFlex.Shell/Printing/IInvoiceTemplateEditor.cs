namespace BestFlex.Shell.Printing
{
    /// <summary>
    /// Optional editor API for saving/loading templates to a persistent store.
    /// </summary>
    public interface IInvoiceTemplateEditor
    {
        PrintTemplate GetTemplateForCompany(int companyId, string docType = "invoice");
        void SetTemplateForCompany(int companyId, PrintTemplate template, string docType = "invoice");
    }
}
