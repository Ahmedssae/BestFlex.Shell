using System;
using System.Threading;
using System.Threading.Tasks;
using BestFlex.Application.Abstractions;  // ✅
using BestFlex.Domain.Entities;          // ✅ Users
using BestFlex.Persistence.Data;         // ✅ DbContext
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace BestFlex.Persistence.Repositories
{
    public sealed class UserRepository : IUserRepository
    {
        private readonly BestFlexDbContext _db;
        private readonly Microsoft.Extensions.Logging.ILogger<UserRepository> _logger;
        public UserRepository(BestFlexDbContext db, Microsoft.Extensions.Logging.ILogger<UserRepository> logger)
        {
            _db = db;
            _logger = logger;
        }

        public async Task<Users?> FindByUsernameAsync(string username, CancellationToken ct = default)
        {
            try
            {
                var conn = _db.Database.GetDbConnection().ConnectionString;
                _logger.LogDebug("UserRepository.FindByUsernameAsync - DB Connection: {Conn}", conn);

                var total = await _db.Users.AsNoTracking().CountAsync(ct);
                _logger.LogDebug("UserRepository: total users = {Count}", total);

                // DEV ONLY: list usernames
                var names = await _db.Users.AsNoTracking().Select(u => u.Username).ToListAsync(ct);
                _logger.LogDebug("UserRepository: users = {Users}", string.Join(',', names));

                if (string.IsNullOrWhiteSpace(username)) return null;
                var n = username.Trim();
                return await _db.Users.AsNoTracking().FirstOrDefaultAsync(u => u.Username == n, ct);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "FindByUsernameAsync failed");
                throw;
            }
        }

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
