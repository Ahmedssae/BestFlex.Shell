using System;
using System.Linq;
using System.Threading.Tasks;
using System.Collections.Generic;
using Microsoft.EntityFrameworkCore;
using BestFlex.Application.Abstractions;
using BestFlex.Persistence.Data; // BestFlexDbContext

namespace BestFlex.Infrastructure.Queries
{
    public sealed class InvoiceQueryService : IInvoiceQueryService
    {
        private readonly BestFlexDbContext _db;

        public InvoiceQueryService(BestFlexDbContext db)
        {
            _db = db;
        }

        public async Task<IReadOnlyList<(int Id, string Name)>> GetAllCustomersAsync()
        {
            // DbSet: CustomerAccounts (Id, Name)
            return await _db.CustomerAccounts
                .AsNoTracking()
                .OrderBy(c => c.Name)
                .Select(c => new ValueTuple<int, string>(c.Id, c.Name))
                .ToListAsync();
        }

        public async Task<PagedResultDto<InvoiceListItemDto>> SearchInvoicesAsync(InvoiceSearchFilter f)
        {
            // Base filtered query over invoices
            var q = _db.SellingInvoices.AsNoTracking().AsQueryable();

            if (f.Start.HasValue) q = q.Where(i => i.IssuedAt >= f.Start.Value);
            if (f.End.HasValue) q = q.Where(i => i.IssuedAt <= f.End.Value);
            if (f.CustomerId.HasValue) q = q.Where(i => i.CustomerAccountId == f.CustomerId.Value);
            if (!string.IsNullOrWhiteSpace(f.NumberLike))
                q = q.Where(i => i.InvoiceNo.Contains(f.NumberLike));

            var total = await q.CountAsync();

            var page = Math.Max(1, f.Page);
            var size = Math.Max(1, f.PageSize);
            var skip = (page - 1) * size;

            // Pre-aggregate items per invoice in a separate query (SQLite-friendly)
            // NOTE: adjust DbSet/props if your names differ.
            var aggItems = _db.SellingInvoiceItems
                .AsNoTracking()
                .GroupBy(x => x.SellingInvoiceId)
                .Select(g => new
                {
                    SellingInvoiceId = g.Key,
                    // Cast BOTH operands to double so SQLite can translate Sum
                    TotalDouble = g.Sum(x => (double)x.UnitPrice * (double)x.Quantity),
                    ItemsCount = g.Count()
                });

            // Page invoices, then LEFT JOIN the aggregates
            var paged = await (
                from i in q
                join a in aggItems on i.Id equals a.SellingInvoiceId into gj
                from a in gj.DefaultIfEmpty()
                orderby i.IssuedAt descending, i.Id descending
                select new
                {
                    i.Id,
                    i.InvoiceNo,
                    i.IssuedAt,
                    CustomerName = i.CustomerAccount.Name,
                    ItemsCount = (int?)(a.ItemsCount) ?? 0,
                    TotalDouble = (double?)(a.TotalDouble) ?? 0.0,
                    i.Currency
                }
            )
            .Skip(skip)
            .Take(size)
            .ToListAsync();

            // Map to DTOs (convert double -> decimal in memory)
            var items = paged.Select(i => new InvoiceListItemDto
            {
                Id = i.Id,
                InvoiceNo = i.InvoiceNo,
                IssuedAt = i.IssuedAt,
                CustomerName = i.CustomerName,
                ItemsCount = i.ItemsCount,
                Total = (decimal)i.TotalDouble,
                Currency = i.Currency
            }).ToList();

            return new PagedResultDto<InvoiceListItemDto>(items, total);
        }
    }
}
