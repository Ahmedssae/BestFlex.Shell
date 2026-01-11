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
            var u = await _db.Users.SingleOrDefaultAsync(x => x.Username == username);
            return u != null && BCryptNet.Verify(password, u.PasswordHash);
        }
    }
}
