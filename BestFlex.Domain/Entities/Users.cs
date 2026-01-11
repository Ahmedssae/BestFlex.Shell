namespace BestFlex.Domain.Entities;

public class Users
{
    public Guid Id { get; set; }
    public string Username { get; set; } = default!;
    public string DisplayName { get; set; } = default!;
    public string PasswordHash { get; set; } = default!;
    public string RolesCsv { get; set; } = "Admin"; // e.g., "Admin,Manager"
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    public DateTime? PasswordChangedAtUtc { get; set; }

    public IEnumerable<string> Roles => (RolesCsv ?? string.Empty)
        .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
}
