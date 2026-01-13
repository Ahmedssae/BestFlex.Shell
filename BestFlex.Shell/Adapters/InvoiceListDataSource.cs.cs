// BestFlex.Shell/Adapters/InvoiceListDataSource.cs
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using BestFlex.Shell.ViewModels; // we'll add this interface next
using BestFlex.Application.Abstractions; // we'll add this interface next

namespace BestFlex.Shell.Adapters
{
    public sealed class InvoiceListDataSource : InvoiceListViewModel.IDataSource
    {
        private readonly IInvoiceQueryService _svc;

        public InvoiceListDataSource(IInvoiceQueryService svc)
        {
            _svc = svc;
        }

        public async Task<ReadOnlyCollection<InvoiceListViewModel.CustomerOption>> LoadCustomersAsync()
        {
            var all = await _svc.GetAllCustomersAsync();
            var mapped = all.Select(c => new InvoiceListViewModel.CustomerOption
            {
                Id = c.Id,
                Name = c.Name
            }).ToList();
            return new ReadOnlyCollection<InvoiceListViewModel.CustomerOption>(mapped);
            // If you want a (All) option: insert one at index 0.
        }

        public async Task<BestFlex.Shell.Infrastructure.PagedResult<InvoiceListViewModel.InvoiceRow>> QueryInvoicesAsync(
            System.DateTime? start, System.DateTime? end, int? customerId, string? numberLike, int pageNumber, int pageSize)
        {
            var result = await _svc.SearchInvoicesAsync(new InvoiceSearchFilter
            {
                Start = start,
                End = end,
                CustomerId = customerId,
                NumberLike = numberLike,
                Page = pageNumber,
                PageSize = pageSize
            });

            var rows = result.Items.Select(i => new InvoiceListViewModel.InvoiceRow
            {
                Id = i.Id,
                InvoiceNo = i.InvoiceNo,
                IssuedAt = i.IssuedAt,
                Customer = i.CustomerName,
                ItemsCount = i.ItemsCount,
                Total = i.Total,
                Currency = i.Currency
            }).ToList();

            return new BestFlex.Shell.Infrastructure.PagedResult<InvoiceListViewModel.InvoiceRow>(
                new ReadOnlyCollection<InvoiceListViewModel.InvoiceRow>(rows),
                result.TotalCount,
                pageNumber,
                pageSize
            );
        }
    }
}
