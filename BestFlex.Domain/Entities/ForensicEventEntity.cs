using System;
namespace BestFlex.Domain.Entities
{
    public sealed class ForensicEventEntity
    {
        public long Id { get; private set; }
        public int EventType { get; private set; }
        public DateTime OccurredAtUtc { get; private set; }
        public string MachineName { get; private set; } = null!;
        public string UserName { get; private set; } = null!;
        public string Description { get; private set; } = null!;
        public string? CorrelationId { get; private set; }
        public string? StackTrace { get; private set; }

        private ForensicEventEntity() { }

        public ForensicEventEntity(int eventType, DateTime occurredAtUtc, string machineName, string userName, string description, string? correlationId, string? stackTrace)
        {
            EventType = eventType;
            OccurredAtUtc = occurredAtUtc;
            MachineName = machineName;
            UserName = userName;
            Description = description;
            CorrelationId = correlationId;
            StackTrace = stackTrace;
        }
    }
}
