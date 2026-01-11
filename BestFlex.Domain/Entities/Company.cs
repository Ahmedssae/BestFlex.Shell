namespace BestFlex.Domain.Entities;

public sealed class Company : EntityBase
{
    public string Name { get; set; } = default!;
    public bool IsOnlineDbEnabled { get; set; }
}
