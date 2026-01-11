using BestFlex.Domain.Entities;

namespace BestFlex.Application.Abstractions
{
    public interface IUserRepository
    {
        Task<Users?> FindByUsernameAsync(string username, CancellationToken ct = default);
        Task<Users?> FindByIdAsync(Guid id, CancellationToken ct = default);
        Task UpdatePasswordHashAsync(Guid id, string newHash, CancellationToken ct = default);
    }
}
