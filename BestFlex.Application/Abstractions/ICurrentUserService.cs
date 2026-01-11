namespace BestFlex.Application.Abstractions;

public interface ICurrentUserService
{
    bool IsSignedIn { get; }
    Guid UserId { get; }
    string Username { get; }
    string DisplayName { get; }
    IReadOnlyList<string> Roles { get; }

    void SignIn(Guid userId, string username, string displayName, IEnumerable<string> roles);
    void SignOut();
}
