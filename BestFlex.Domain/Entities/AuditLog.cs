using System;

namespace BestFlex.Domain.Entities
{
    public class AuditLog
    {
        public int Id { get; set; }
        public int UserId { get; set; }
        public string Username { get; set; } = default!;
        public string Action { get; set; } = default!;
        public string? Entity { get; set; }
        public int? EntityId { get; set; }
        public string? Details { get; set; }
        public DateTime TimestampUtc { get; set; } = DateTime.UtcNow;
    }
}
