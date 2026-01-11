namespace BestFlex.Shell.Printing
{
    public interface IInvoiceTemplateProvider
    {
        PrintTemplate GetTemplateForCompany(int companyId);
    }
}
