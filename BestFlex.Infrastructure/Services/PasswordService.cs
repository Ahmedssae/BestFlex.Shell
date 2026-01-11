using System;
using System.Threading.Tasks;
using BCryptNet = BCrypt.Net.BCrypt;
using BestFlex.Persistence.Data;
using Microsoft.EntityFrameworkCore;

namespace BestFlex.Infrastructure.Services
{
    public sealed class PasswordService
    {
        private readonly BestFlexDbContext _db;
        public PasswordService(BestFlexDbContext db) => _db = db;

        public async Task<bool> ChangePasswordAsync(Guid userId, string currentPassword, string newPassword)
        {
            var user = await _db.Users.FirstOrDefaultAsync(u => u.Id == userId);
            if (user == null) return false;

            if (!BCryptNet.Verify(currentPassword, user.PasswordHash))
                return false;

            user.PasswordHash = BCryptNet.HashPassword(newPassword);
            user.PasswordChangedAtUtc = DateTime.UtcNow;

            await _db.SaveChangesAsync();
            return true;
        }
    }
}
