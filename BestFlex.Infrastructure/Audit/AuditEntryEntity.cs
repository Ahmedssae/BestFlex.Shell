using System;
using BestFlex.Domain.Entities;

namespace BestFlex.Infrastructure.Audit
{
    public class AuditEntryEntity
    {
        public Guid Id { get; set; }
        public string Action { get; set; } = string.Empty;
        public string EntityName { get; set; } = string.Empty;
        public string EntityId { get; set; } = string.Empty;
        public string UserId { get; set; } = string.Empty;
        public DateTime TimestampUtc { get; set; }
        public string Details { get; set; } = string.Empty;
    }
}
