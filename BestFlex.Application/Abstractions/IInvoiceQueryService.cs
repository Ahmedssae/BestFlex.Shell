// BestFlex.Application/Abstractions/IInvoiceQueryService.cs
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace BestFlex.Application.Abstractions
{
    public sealed class InvoiceSearchFilter
    {
        public DateTime? Start { get; set; }
        public DateTime? End { get; set; }
        public int? CustomerId { get; set; }
        public string? NumberLike { get; set; }
        public int Page { get; set; } = 1;      // 1-based
        public int PageSize { get; set; } = 25; // >0
    }

    public sealed class InvoiceListItemDto
    {
        public int Id { get; set; }
        public string InvoiceNo { get; set; } = "";
        public DateTime IssuedAt { get; set; }
        public string CustomerName { get; set; } = "";
        public int ItemsCount { get; set; }
        public decimal Total { get; set; }
        public string Currency { get; set; } = "";
    }

    public sealed class PagedResultDto<T>
    {
        public IReadOnlyList<T> Items { get; }
        public int TotalCount { get; }
        public PagedResultDto(IReadOnlyList<T> items, int totalCount)
        {
            Items = items;
            TotalCount = totalCount;
        }
    }

    public interface IInvoiceQueryService
    {
        Task<IReadOnlyList<(int Id, string Name)>> GetAllCustomersAsync();

        Task<PagedResultDto<InvoiceListItemDto>> SearchInvoicesAsync(InvoiceSearchFilter filter);
    }
}
