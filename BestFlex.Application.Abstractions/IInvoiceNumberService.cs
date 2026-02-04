using System.Threading.Tasks;

namespace BestFlex.Application.Abstractions
{
    public interface IInvoiceNumberService
    {
        Task<string> NextAsync(int companyId);
    }
}
