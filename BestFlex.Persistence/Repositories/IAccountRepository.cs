using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using BestFlex.Domain.Entities;

namespace BestFlex.Persistence.Repositories
{
    public interface IAccountRepository
    {
        Task<List<Account>> GetRequiredAccountsAsync(CancellationToken cancellationToken = default);
    }
}
