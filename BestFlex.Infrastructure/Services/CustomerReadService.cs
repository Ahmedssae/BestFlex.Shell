using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using BestFlex.Application.Abstractions;
using BestFlex.Persistence.Data;
using Microsoft.EntityFrameworkCore;

namespace BestFlex.Infrastructure.Services
{
    public sealed class CustomerReadService : ICustomerReadService
    {
        private readonly BestFlexDbContext _db;
        
        public CustomerReadService(BestFlexDbContext db) => _db = db;

        public async Task<List<CustomerLookupDto>> GetForSalesAsync(CancellationToken ct = default)
        {
            return await _db.CustomerAccounts
                .AsNoTracking()
                .OrderBy(c => c.Name)
                .Select(c => new CustomerLookupDto
                {
                    Id = c.Id,
                    Name = c.Name
                })
                .ToListAsync(ct);
        }
    }
}
