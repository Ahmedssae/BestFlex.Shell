namespace BestFlex.Shell.Security
{
    public interface IAuthorizationService
    {
        bool HasRole(string role);
        bool IsAdmin { get; }
    }
}
