using System;
using System.Linq;
using System.Collections.Generic;

namespace BestFlex.Shell.Infrastructure
{
    public sealed class PagedResult<T>
    {
        public IReadOnlyList<T> Items { get; }
        public int TotalCount { get; }
        public int PageIndex { get; }
        public int PageSize { get; }

        public PagedResult(IReadOnlyList<T> items, int totalCount, int pageIndex, int pageSize)
        {
            Items = items;
            TotalCount = totalCount;
            PageIndex = pageIndex;
            PageSize = pageSize;
        }
    }

    public sealed class PaginationState
    {
        public int PageIndex { get; private set; } = 1; // 1-based
        public int PageSize { get; private set; } = 25;
        public int TotalCount { get; private set; }
        public bool HasPrevious => PageIndex > 1;
        public bool HasNext => PageIndex < Math.Max(1, (int)Math.Ceiling(TotalCount / (double)PageSize));

        public void Update(int pageIndex, int pageSize, int totalCount)
        {
            PageIndex = Math.Max(1, pageIndex);
            PageSize = Math.Max(1, pageSize);
            TotalCount = Math.Max(0, totalCount);
        }
    }

    public static class QueryablePagingExtensions
    {
        public static IQueryable<T> ApplyPaging<T>(this IQueryable<T> q, int pageIndex, int pageSize)
        {
            if (pageIndex <= 1) return q.Take(pageSize);
            return q.Skip((pageIndex - 1) * pageSize).Take(pageSize);
        }
    }
}
