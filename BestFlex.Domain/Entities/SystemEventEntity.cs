using System;

namespace BestFlex.Domain.Entities
{
    public sealed class SystemEventEntity
    {
        public Guid Id { get; set; }
        public DateTime OccurredAtUtc { get; set; }
        public string Severity { get; set; } = default!;
        public string Source { get; set; } = default!;
        public string Message { get; set; } = default!;
        public string? ExceptionType { get; set; }
        public string? StackTrace { get; set; }
    }
}
