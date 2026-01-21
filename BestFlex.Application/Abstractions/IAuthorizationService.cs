using System.Threading.Tasks;

namespace BestFlex.Application.Abstractions
{
    public interface IAuthorizationService
    {
        Task<bool> HasPermissionAsync(Permission permission);
        Task<bool> HasAnyPermissionAsync(params Permission[] permissions);
        Task<bool> HasAllPermissionsAsync(params Permission[] permissions);
        Task<string?> GetPermissionDeniedReasonAsync(Permission permission);
        Task<string?> GetCurrentRoleAsync();
    }
}
