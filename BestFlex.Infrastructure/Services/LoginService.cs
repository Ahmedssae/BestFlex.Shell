using System.Threading.Tasks;
using BestFlex.Persistence.Data;
using Microsoft.EntityFrameworkCore;
using BCryptNet = BCrypt.Net.BCrypt;

namespace BestFlex.Infrastructure.Services

{
    public class LoginService
    {
        private readonly BestFlexDbContext _db;
        public LoginService(BestFlexDbContext db) => _db = db;

        public async Task<bool> ValidateAsync(string username, string password)
        {
            if (string.IsNullOrWhiteSpace(username)) return false;
            var n = username.Trim().ToLowerInvariant();
            var u = await _db.Users.FirstOrDefaultAsync(x => x.Username.ToLower() == n);
            if (u == null) return false;
            try
            {
                return BCryptNet.Verify(password ?? string.Empty, u.PasswordHash);
            }
            catch
            {
                return false;
            }
        }
    }
}
