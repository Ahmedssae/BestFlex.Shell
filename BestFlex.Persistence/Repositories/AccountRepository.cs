using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using BestFlex.Domain.Entities;
using BestFlex.Persistence.Data;
using Microsoft.EntityFrameworkCore;

namespace BestFlex.Persistence.Repositories
{
    public class AccountRepository : IAccountRepository
    {
        private readonly BestFlexDbContext _db;

        public AccountRepository(BestFlexDbContext db)
        {
            _db = db ?? throw new ArgumentNullException(nameof(db));
        }

        public async Task<List<Account>> GetRequiredAccountsAsync(CancellationToken cancellationToken = default)
        {
            return await _db.Accounts
                .Where(a => a.IsActive)
                .ToListAsync(cancellationToken);
        }
    }
}
