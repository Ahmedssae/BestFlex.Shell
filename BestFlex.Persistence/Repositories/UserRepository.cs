using System;
using System.Threading;
using System.Threading.Tasks;
using BestFlex.Application.Abstractions;  // ✅
using BestFlex.Domain.Entities;          // ✅ Users
using BestFlex.Persistence.Data;         // ✅ DbContext
using Microsoft.EntityFrameworkCore;

namespace BestFlex.Persistence.Repositories
{
    public sealed class UserRepository : IUserRepository
    {
        private readonly BestFlexDbContext _db;
        public UserRepository(BestFlexDbContext db) => _db = db;

        public Task<Users?> FindByUsernameAsync(string username, CancellationToken ct = default)
            => _db.Users.AsNoTracking().FirstOrDefaultAsync(u => u.Username == username, ct);

        public Task<Users?> FindByIdAsync(Guid id, CancellationToken ct = default)
            => _db.Users.FirstOrDefaultAsync(u => u.Id == id, ct);

        public async Task UpdatePasswordHashAsync(Guid id, string newHash, CancellationToken ct = default)
        {
            var user = await _db.Users.FirstOrDefaultAsync(u => u.Id == id, ct);
            if (user is null) return;
            user.PasswordHash = newHash;
            user.PasswordChangedAtUtc = DateTime.UtcNow;
            await _db.SaveChangesAsync(ct);
        }
    }
}
