using System.Threading.Tasks;

namespace BestFlex.Shell.Services
{
    public interface IInvoiceNumberService
    {
        Task<string> NextAsync(int companyId);
    }
}
