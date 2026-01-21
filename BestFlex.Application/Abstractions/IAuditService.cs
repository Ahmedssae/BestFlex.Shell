using System.Threading.Tasks;

namespace BestFlex.Application.Abstractions
{
    public interface IAuditService
    {
        Task LogActionAsync(string action, string? entity = null, int? entityId = null);
        Task LogSecurityAsync(string action, string? details = null);
        Task LogNavigationAsync(string destination);
    }
}
